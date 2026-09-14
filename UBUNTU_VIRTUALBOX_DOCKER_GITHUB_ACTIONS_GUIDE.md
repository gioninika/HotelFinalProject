# Deploying the Hotel API: Ubuntu VM, Docker, SSH, and GitHub Actions

This guide is tailored to this repository (`gioninika/HotelFinalProject`): an ASP.NET API in `Hotel/` and a SQL Server database. It uses a **local Ubuntu Server virtual machine** in Oracle VirtualBox.

## The deployment design

```
Windows/phone on your LAN ── SSH (<VM_LAN_IP>:22) ──> Ubuntu VM
            Termius

Git push to main ──> GitHub Actions build runner ──> GHCR container image
                                                    │
                                             outbound HTTPS only
                                                    │
Ubuntu VM self-hosted Actions runner ── pulls image ──> Docker Compose
```

This is deliberately a self-hosted deployment runner inside the VM. GitHub-hosted runners cannot reach an Ubuntu VM at a private home/office LAN address. The recommended design requires no public IP, no router port forwarding, and no SSH private key stored in GitHub.

This is an excellent local/staging deployment. It is **not** a public production site yet. For a public site, move the same Docker setup to a cloud VM with a domain, HTTPS reverse proxy, firewall, backups, and monitoring.

## 1. Create the Ubuntu Server VM

1. Enable CPU virtualization (Intel VT-x/AMD-V) in the Windows BIOS/UEFI if VirtualBox says it is unavailable.
2. Download the current 64-bit Ubuntu **Server LTS** ISO from [Ubuntu](https://ubuntu.com/download/server), and install the current VirtualBox release from [Oracle](https://www.virtualbox.org/wiki/Downloads).
3. In VirtualBox, select **New** and use these sensible starting values:

   | Setting | Value |
   | --- | --- |
   | Name | `hotel-ubuntu` |
   | ISO | the Ubuntu Server LTS ISO |
   | Type/version | Linux / Ubuntu (64-bit) |
   | CPUs | 4 (use 2 if the Windows host is small) |
   | RAM | 8 GB (4 GB minimum; SQL Server benefits from more) |
   | Disk | 50 GB dynamically allocated VDI |
   | Network | Bridged Adapter — select the physical Wi-Fi or Ethernet adapter that reaches your LAN |

4. Start the VM. In the Ubuntu installer, choose a normal server installation, create a normal administrator account (for example `admin`), select **Install OpenSSH server**, and finish the install. Remove the ISO when asked and reboot.
5. Log in from the VM console, update it, and create a separate deployment account:

   ```bash
   sudo apt update
   sudo apt full-upgrade -y
   sudo apt install -y openssh-server ufw unattended-upgrades
   sudo systemctl enable --now ssh

   sudo adduser deploy
   sudo usermod -aG sudo deploy

   ```

   Use the administrator account for recovery and `deploy` for normal deployments. Do not use `root` for SSH.

## 2. Use a bridged LAN connection

A bridged VM appears as a separate device on your local network. This lets you use Termius from the Windows host or another trusted LAN device without a VirtualBox port-forward rule.

1. Shut down the VM. In **VM Settings → Network → Adapter 1**, set **Attached to** to **Bridged Adapter**, then select the Windows computer's active physical **Wi-Fi** or **Ethernet** adapter. Keep **Cable Connected** checked. Do not select a VPN, Bluetooth, or virtual adapter.
2. Start the VM and find its LAN IP address:

   ```bash
   hostname -I
   ip -4 addr show
   ```

   Use the address that is on the same network as Windows, for example `192.168.1.50`. Call it `<VM_LAN_IP>` below. Do not use `127.0.0.1`, `10.0.2.15` (the usual NAT address), or a Docker `172.*` address.
3. Reserve that address in your router's DHCP settings using the VM's MAC address shown in **VirtualBox → Network → Advanced**. A reservation prevents Termius and the API URL from breaking when the VM is restarted.
4. Find your LAN range. On Windows PowerShell, run:

   ```powershell
   Get-NetIPAddress -AddressFamily IPv4 | Format-Table IPAddress,PrefixLength,InterfaceAlias
   ```

   For example, an IP of `192.168.1.20` with a prefix length of `24` means the LAN range is `192.168.1.0/24`. Replace `<YOUR_LAN_CIDR>` in the following commands with your real range.
5. In the VM console, enable the firewall and allow only your LAN to reach SSH and the local API:

   ```bash
   sudo ufw default deny incoming
   sudo ufw default allow outgoing
   sudo ufw allow from <YOUR_LAN_CIDR> to any port 22 proto tcp
   sudo ufw allow from <YOUR_LAN_CIDR> to any port 8080 proto tcp
   sudo ufw enable
   sudo ufw status numbered
   ```

   Do not create any VirtualBox NAT port-forward rules and do not forward ports 22 or 8080 from your Internet router. Bridged networking exposes the VM to the LAN, so restrict access with the firewall and use only a trusted network.
6. From Windows PowerShell, confirm the initial password connection and network reachability:

   ```powershell
   Test-NetConnection <VM_LAN_IP> -Port 22
   ssh admin@<VM_LAN_IP>
   ```

## 3. Create a safe SSH key and connect with Termius

This guide assumes “Terminus” means **Termius**, the SSH client. If you mean a different terminal program, use the same address, port, username, and private key shown below.

### Create the management key on Windows

Run this **on Windows PowerShell**, not in the VM:

```powershell
ssh-keygen -t ed25519 -a 100 -f "$env:USERPROFILE\.ssh\hotel-vm-ed25519" -C "hotel-vm-admin"
```

Give the key a passphrase. It creates:

* `hotel-vm-ed25519` — the private key; never send it, commit it, or add it to GitHub.
* `hotel-vm-ed25519.pub` — the public key; this is safe to copy to the server.

Install the public key using the password-based SSH session that you already verified:

```powershell
$VmIp = "<VM_LAN_IP>"
Get-Content "$env:USERPROFILE\.ssh\hotel-vm-ed25519.pub" |
  ssh "admin@$VmIp" "umask 077; mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys; chmod 700 ~/.ssh; chmod 600 ~/.ssh/authorized_keys"
```

Open a **second** PowerShell window and test the key before changing SSH security settings:

```powershell
ssh -i "$env:USERPROFILE\.ssh\hotel-vm-ed25519" admin@<VM_LAN_IP>
```

Only after it succeeds, create `/etc/ssh/sshd_config.d/99-hotel-hardening.conf` in the VM with the following contents (replace the username if yours is different):

```text
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
X11Forwarding no
MaxAuthTries 3
AllowUsers admin deploy
```

Validate, then apply the change:

```bash
sudo sshd -t && sudo systemctl reload ssh
```

Keep the working SSH session open until you have verified a new key-based connection. This avoids locking yourself out.

### Add the host in Termius

1. Create/import an Identity using the private file `C:\Users\<your-Windows-user>\.ssh\hotel-vm-ed25519`. Enter its passphrase and keep the Termius vault protected with Windows Hello/MFA if available.
2. Add a new Host with:

   ```text
   Label: Hotel Ubuntu VM
   Address: <VM_LAN_IP>   (for example, 192.168.1.50)
   Port: 22
   Username: admin
   Identity: hotel-vm-ed25519
   ```

3. On first connection, compare the displayed host fingerprint with this command run in the VM console:

   ```bash
   sudo ssh-keygen -l -f /etc/ssh/ssh_host_ed25519_key.pub
   ```

Never ignore an unexpected host-key change; it can signal that you are connecting to a different machine.

## 4. Install Docker Engine and Compose

Run these commands in the Ubuntu VM. They use Docker's official Ubuntu repository rather than Ubuntu's older `docker.io` package:

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo \"${UBUNTU_CODENAME:-$VERSION_CODENAME}\") stable" |
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker deploy
sudo systemctl enable --now docker
```

Log out and back in as `deploy`, then verify:

```bash
docker run --rm hello-world
docker compose version
```

Membership of the `docker` group effectively grants administrator-level access to the VM. Keep that group limited to `deploy` and trusted administrators.

## 5. Prepare a production Compose definition

The repository's current `docker-compose.yml` is a development file. It builds locally, runs `Development`, and embeds database/JWT secrets. Do **not** deploy it unchanged, and rotate the secrets that are currently committed in `Hotel/appsettings.json` and the development Compose file.

Add this new tracked file as `docker-compose.prod.yml` in the repository. It pulls the immutable published app image and receives secrets only from the VM's protected environment file:

```yaml
name: hotels-management

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:?Set it in /opt/hotel/.env}
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q \"SELECT 1\""]
      interval: 5s
      timeout: 3s
      retries: 30
      start_period: 15s

  hotel:
    image: ${IMAGE:?Set the GHCR image in /opt/hotel/.env}
    pull_policy: always
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=HotelsManagementDb;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true"
      Jwt__Key: ${JWT_KEY:?Set it in /opt/hotel/.env}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      # Fine for this one API replica during initial setup. Back up first.
      Database__ApplyMigrations: "true"
    depends_on:
      sqlserver:
        condition: service_healthy
    ports:
      - "8080:8080"

volumes:
  sqlserver-data:
```

Create the protected runtime directory and configuration in the VM:

```bash
sudo install -d -o deploy -g deploy -m 0750 /opt/hotel
sudo -u deploy nano /opt/hotel/.env
sudo chmod 600 /opt/hotel/.env
```

Put the following values in `/opt/hotel/.env`, replacing every placeholder. Use a unique SQL password that has upper- and lower-case letters, a number, and a symbol. `openssl rand -hex 64` is suitable for `JWT_KEY`.

```dotenv
IMAGE=ghcr.io/gioninika/hotelfinalproject:production
MSSQL_SA_PASSWORD=replace-with-a-unique-long-sql-password
JWT_KEY=replace-with-a-unique-64-byte-hex-secret
JWT_ISSUER=HotelsManagementApi
JWT_AUDIENCE=HotelsManagementApi
```

Do not put this file in Git, GitHub Actions secrets, an image, or Dockerfile. Add `/opt/hotel/.env` to your backups only if those backups are encrypted and access-controlled. Add `.env` to the repository's `.gitignore` as an additional safeguard.

`Database__ApplyMigrations=true` makes the current application run EF Core migrations at startup. That is acceptable for this single local API, but make a database backup before every deploy. For multiple replicas or a public production service, migrate in a dedicated, reviewed release step instead.

## 6. Publish the Docker image and deploy it with GitHub Actions

### Set up GitHub Container Registry

1. Push the repository to GitHub and make it **private** while you use a self-hosted runner. GitHub warns that untrusted pull requests can execute dangerous code on self-hosted runners.
2. In GitHub, create the `production` environment: **Settings → Environments → New environment**. Require approval if another person should approve deployments, and restrict it to the protected `main` branch.
3. Enable branch protection for `main`: require pull requests/reviews as appropriate and require the build check to pass.
4. Add `.github/workflows/deploy.yml` with this workflow. The build happens on a clean GitHub-hosted runner; the deployment job runs in the Ubuntu VM and pulls the production image.

```yaml
name: Build and deploy Hotel API

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: hotel-production
  cancel-in-progress: false

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ghcr.io/gioninika/hotelfinalproject

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x
      - run: dotnet test Hotel.slnx --configuration Release

  build:
    needs: verify
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v6
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/build-push-action@v6
        with:
          context: .
          file: Hotel/Dockerfile
          push: true
          tags: |
            ${{ env.IMAGE_NAME }}:production
            ${{ env.IMAGE_NAME }}:sha-${{ github.sha }}

  deploy:
    needs: build
    runs-on: [self-hosted, linux, x64, hotel-prod]
    environment: production
    permissions:
      contents: read
      packages: read
    steps:
      - uses: actions/checkout@v6
      - name: Pull and start the release
        env:
          GHCR_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          ENV_FILE: /opt/hotel/.env
        run: |
          printf '%s' "$GHCR_TOKEN" | docker login ghcr.io -u "${{ github.actor }}" --password-stdin
          docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml pull
          docker compose --env-file "$ENV_FILE" -f docker-compose.prod.yml up -d --remove-orphans
      - name: Remove temporary registry credentials
        if: always()
        run: docker logout ghcr.io
```

The `GITHUB_TOKEN` is short-lived and scoped to the job. It publishes and later pulls the package associated with this repository; no long-lived Docker password or CI SSH key is necessary. In a hardened production workflow, pin every third-party action to a reviewed commit SHA rather than a mutable version tag.

### Register the Ubuntu VM as the self-hosted runner

1. On GitHub, open the repository: **Settings → Actions → Runners → New self-hosted runner**.
2. Select **Linux** and **x64**. GitHub displays a download command and a one-hour registration token.
3. In the VM, create a runner directory owned by `deploy`:

   ```bash
   sudo install -d -o deploy -g deploy -m 0750 /opt/actions-runner
   sudo -iu deploy
   cd /opt/actions-runner
   ```

4. Paste the download/extract commands GitHub displays. When running its configuration command, add the extra label used in the workflow:

   ```bash
   ./config.sh --url https://github.com/gioninika/HotelFinalProject --token YOUR_ONE_HOUR_TOKEN --labels hotel-prod
   ```

5. Still in `/opt/actions-runner`, install and start the runner service:

   ```bash
   sudo ./svc.sh install deploy
   sudo ./svc.sh start
   sudo ./svc.sh status
   ```

6. Confirm GitHub shows the runner as **Idle**. Push a small, reviewed change to `main`; the workflow should publish the image, then the VM should run the `deploy` job.

The runner service must run as `deploy`, because that user has access to Docker and `/opt/hotel/.env`. It accepts only the trusted workflow above; do not use this runner for arbitrary repositories, public fork pull requests, or experimental Actions.

## 7. Verify and troubleshoot a deployment

In the Ubuntu VM/Termius session:

```bash
docker compose --env-file /opt/hotel/.env -f docker-compose.prod.yml ps
docker compose --env-file /opt/hotel/.env -f docker-compose.prod.yml logs --tail=200 hotel
docker compose --env-file /opt/hotel/.env -f docker-compose.prod.yml logs --tail=200 sqlserver
curl http://127.0.0.1:8080/api/hotels
```

From Windows or another device allowed by the LAN firewall, the API is reachable at the VM's LAN address:

```powershell
curl http://<VM_LAN_IP>:8080/api/hotels
```

If the API fails at first start, wait for the SQL Server health check, then inspect the API logs. Most initial failures are an SQL password that does not satisfy SQL Server's complexity rules, a bad connection string, a missing GHCR package permission, or a migration error.

To roll back an application image after a bad deploy, use the commit image that the workflow already created:

```bash
IMAGE=ghcr.io/gioninika/hotelfinalproject:sha-REPLACE_WITH_FULL_COMMIT_SHA \
  docker compose --env-file /opt/hotel/.env -f docker-compose.prod.yml up -d
```

Back up the SQL Server volume before schema-changing releases. The database lives in Docker's `sqlserver-data` volume; deleting that volume destroys the data.

## 8. Security checklist before calling it production

- [ ] Rotate the currently committed database password and JWT key; remove them from tracked configuration.
- [ ] Use different SSH keys for each person/device. Revoke a lost key by removing only its public-key line from `authorized_keys`.
- [ ] Keep the Termius key private, passphrase-protected, and out of GitHub.
- [ ] Keep the VM on a trusted LAN, reserve its DHCP address, restrict UFW rules to that LAN, and do not forward SSH from an Internet router to this VM.
- [ ] Restrict the self-hosted runner to a private repository and protected `main` branch.
- [ ] Set up encrypted database backups and test a restore.
- [ ] For a public release, use a public cloud VM, a domain, Caddy/Nginx, TLS certificates, and a restrictive firewall. `UseHttpsRedirection()` in the app does not itself provision an HTTPS certificate or reverse proxy.
- [ ] Review GitHub Actions logs after the first release and never print secrets in workflow commands.

## Optional: a separate CI SSH key for a real remote server

Do this only if you later deploy from a GitHub-hosted runner to a cloud VM with a public DNS/IP and a firewall. It will **not** make a local VirtualBox VM on your private LAN reachable from GitHub.

Create a separate, dedicated key on a secure administrator machine:

```bash
ssh-keygen -t ed25519 -a 100 -f github-actions-hotel-deploy -C github-actions-hotel-deploy
```

Add only its `.pub` file to the remote server's `deploy` user's `~/.ssh/authorized_keys`, and put only the private key in the GitHub `production` environment secret named `DEPLOY_SSH_KEY`. Never reuse the Termius personal key. Limit the SSH account and firewall to the minimum necessary; the self-hosted runner approach above is safer for this local VM.

## Official references

- [Ubuntu Server download](https://ubuntu.com/download/server)
- [Oracle VirtualBox networking manual](https://www.virtualbox.org/manual/ch06.html)
- [Docker Engine installation for Ubuntu](https://docs.docker.com/engine/install/ubuntu/)
- [GitHub: publish Docker images](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images)
- [GitHub: add a self-hosted runner](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners)
- [GitHub: manage deploy keys](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/managing-deploy-keys)

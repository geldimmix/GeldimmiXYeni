#!/bin/bash
# Geldimmi Server Setup Script
# Run this on geldimmi.com server as root

echo "=== Geldimmi Server Setup ==="

# Update system
apt update && apt upgrade -y

# Install required packages
apt install -y wget curl apt-transport-https software-properties-common

# Install .NET 9.0 Runtime
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt update
apt install -y aspnetcore-runtime-9.0

# Install Nginx
apt install -y nginx

# Create app directory
mkdir -p /var/www/geldimmi
chown -R www-data:www-data /var/www/geldimmi

# Create Nginx config
cat > /etc/nginx/sites-available/geldimmi << 'EOF'
server {
    listen 80;
    server_name geldimmi.com www.geldimmi.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF

# Enable site
ln -sf /etc/nginx/sites-available/geldimmi /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default

# Test and reload nginx
nginx -t && systemctl reload nginx

# Install Certbot for SSL
apt install -y certbot python3-certbot-nginx

# Get SSL certificate
certbot --nginx -d geldimmi.com -d www.geldimmi.com --non-interactive --agree-tos -m algoritmauzmani@gmail.com

# Enable firewall
ufw allow 'Nginx Full'
ufw allow OpenSSH
ufw --force enable

echo "=== Setup Complete ==="
echo "Server is ready for deployment!"
echo ""
echo "Nginx status:"
systemctl status nginx --no-pager



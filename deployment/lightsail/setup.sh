#!/bin/bash
# Script de configuración para AWS Lightsail
# Sistema: Ubuntu 22.04 LTS
# Plan recomendado: Small (1 GB RAM, 1 vCPU, $5/mes)

set -e

echo "=== Instalación de E-Supplier en AWS Lightsail ==="
echo ""

# Actualizar sistema
echo "1. Actualizando sistema operativo..."
sudo apt update && sudo apt upgrade -y

# Instalar dependencias
echo "2. Instalando dependencias..."
sudo apt install -y wget apt-transport-https software-properties-common nginx

# Instalar .NET 8 SDK y Runtime
echo "3. Instalando .NET 8..."
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt update
sudo apt install -y dotnet-sdk-8.0 dotnet-runtime-8.0 aspnetcore-runtime-8.0

# Verificar instalación
dotnet --version

# Crear usuario para la aplicación
echo "4. Creando usuario para la aplicación..."
sudo useradd -m -s /bin/bash esupplier || echo "Usuario ya existe"

# Crear directorios
echo "5. Creando estructura de directorios..."
sudo mkdir -p /var/www/esupplier
sudo mkdir -p /var/log/esupplier
sudo chown -R esupplier:esupplier /var/www/esupplier
sudo chown -R esupplier:esupplier /var/log/esupplier

# Configurar Nginx como reverse proxy
echo "6. Configurando Nginx..."
sudo tee /etc/nginx/sites-available/esupplier > /dev/null <<'NGINX_CONFIG'
server {
    listen 80;
    listen [::]:80;
    server_name _;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
    
    # Swagger
    location /swagger {
        proxy_pass http://localhost:5000/swagger;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    
    # Health check
    location /health {
        proxy_pass http://localhost:5000/health;
        access_log off;
    }
}
NGINX_CONFIG

# Activar sitio
sudo ln -sf /etc/nginx/sites-available/esupplier /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default

# Verificar configuración de Nginx
sudo nginx -t

# Reiniciar Nginx
sudo systemctl restart nginx
sudo systemctl enable nginx

# Crear servicio systemd
echo "7. Configurando servicio systemd..."
sudo tee /etc/systemd/system/esupplier.service > /dev/null <<'SYSTEMD_CONFIG'
[Unit]
Description=E-Supplier ASP.NET Core Web API
After=network.target

[Service]
Type=notify
User=esupplier
WorkingDirectory=/var/www/esupplier
ExecStart=/usr/bin/dotnet /var/www/esupplier/esupplier.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=esupplier
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://localhost:5000

# Logs
StandardOutput=append:/var/log/esupplier/stdout.log
StandardError=append:/var/log/esupplier/stderr.log

[Install]
WantedBy=multi-user.target
SYSTEMD_CONFIG

# Recargar systemd
sudo systemctl daemon-reload

# Configurar firewall
echo "8. Configurando firewall..."
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable

# Instalar certbot para SSL (opcional)
echo "9. Instalando Certbot para SSL..."
sudo apt install -y certbot python3-certbot-nginx

echo ""
echo "=== Instalación completada ==="
echo ""
echo "Próximos pasos:"
echo "1. Sube tu aplicación compilada a /var/www/esupplier/"
echo "   - Puedes usar: scp -r ./bin/Release/net8.0/publish/* user@ip:/var/www/esupplier/"
echo ""
echo "2. Ajusta permisos:"
echo "   - sudo chown -R esupplier:esupplier /var/www/esupplier"
echo ""
echo "3. Inicia el servicio:"
echo "   - sudo systemctl start esupplier"
echo "   - sudo systemctl enable esupplier"
echo ""
echo "4. Verifica el estado:"
echo "   - sudo systemctl status esupplier"
echo "   - sudo journalctl -u esupplier -f"
echo ""
echo "5. Configura SSL (recomendado):"
echo "   - sudo certbot --nginx -d tu-dominio.com"
echo ""
echo "6. Verifica logs:"
echo "   - tail -f /var/log/esupplier/stdout.log"
echo "   - tail -f /var/log/esupplier/stderr.log"
echo ""


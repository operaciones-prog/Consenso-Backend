#!/bin/bash
# Script de deployment automático para Lightsail
# Ejecutar desde tu máquina local

set -e

# Configuración
SERVER_IP="TU_IP_LIGHTSAIL"
SERVER_USER="ubuntu"
APP_NAME="esupplier"
LOCAL_PUBLISH_DIR="./bin/Release/net8.0/publish"
REMOTE_APP_DIR="/var/www/esupplier"

echo "=== Deployment de E-Supplier a AWS Lightsail ==="
echo ""

# Compilar aplicación
echo "1. Compilando aplicación..."
dotnet publish -c Release -o $LOCAL_PUBLISH_DIR

# Verificar que la compilación fue exitosa
if [ ! -d "$LOCAL_PUBLISH_DIR" ]; then
    echo "Error: No se encontró el directorio de publicación"
    exit 1
fi

echo "2. Deteniendo servicio remoto..."
ssh $SERVER_USER@$SERVER_IP "sudo systemctl stop esupplier || true"

# Backup de la versión anterior
echo "3. Creando backup de la versión anterior..."
ssh $SERVER_USER@$SERVER_IP "sudo mkdir -p /var/backups/esupplier && sudo cp -r $REMOTE_APP_DIR /var/backups/esupplier/backup-\$(date +%Y%m%d-%H%M%S) || true"

# Subir archivos
echo "4. Subiendo archivos al servidor..."
rsync -avz --delete \
    --exclude 'appsettings.Development.json' \
    --exclude 'appsettings.Release.json' \
    $LOCAL_PUBLISH_DIR/ $SERVER_USER@$SERVER_IP:$REMOTE_APP_DIR/

# Ajustar permisos
echo "5. Ajustando permisos..."
ssh $SERVER_USER@$SERVER_IP "sudo chown -R esupplier:esupplier $REMOTE_APP_DIR"

# Reiniciar servicio
echo "6. Iniciando servicio..."
ssh $SERVER_USER@$SERVER_IP "sudo systemctl start esupplier"

# Esperar a que el servicio inicie
echo "7. Esperando inicio del servicio..."
sleep 5

# Verificar estado
echo "8. Verificando estado del servicio..."
ssh $SERVER_USER@$SERVER_IP "sudo systemctl status esupplier --no-pager"

# Verificar logs
echo ""
echo "9. Últimas líneas del log:"
ssh $SERVER_USER@$SERVER_IP "sudo tail -20 /var/log/esupplier/stdout.log"

echo ""
echo "=== Deployment completado ==="
echo ""
echo "Para ver logs en tiempo real:"
echo "  ssh $SERVER_USER@$SERVER_IP 'sudo journalctl -u esupplier -f'"
echo ""
echo "Para verificar la aplicación:"
echo "  curl http://$SERVER_IP/swagger"
echo ""


# ============================================================================
# SmartLog Analyzer - Script de depuración del flujo completo
# ============================================================================
# Este script inicia la aplicación, ejecuta una secuencia de peticiones HTTP
# a todos los endpoints y muestra las respuestas para verificar el flujo completo.
# 
# Uso: .\scripts\debug-flow.ps1
# ============================================================================

$ErrorActionPreference = "Continue"
$baseUrl = "http://localhost:5090"
$logFile = "C:\Users\ADRIA\AppData\Local\Temp\opencode\smartlog-debug.log"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SmartLog Analyzer - Depuración de flujo" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Matar procesos dotnet anteriores ---
Write-Host "[1/8] Terminando procesos dotnet anteriores..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# --- 2. Iniciar la aplicación ---
Write-Host "[2/8] Iniciando la aplicación..." -ForegroundColor Yellow
$proc = Start-Process powershell -ArgumentList '-NoExit','-Command',"dotnet run --project src/SmartLogAnalyzer.Api/SmartLogAnalyzer.Api.csproj --urls `"$baseUrl`" > $logFile 2>&1" -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 15

# Verificar que la app esté corriendo
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -Method Get -ErrorAction Stop
    Write-Host "App iniciada correctamente. Health: $($health | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "ERROR: La app no responde. Verificar logs en $logFile" -ForegroundColor Red
    exit 1
}

# --- 3. Ingerir logs de prueba ---
Write-Host ""
Write-Host "[3/8] Ingestando logs de prueba..." -ForegroundColor Yellow

$log1 = @{
    serviceName = "payments"
    level = "Error"
    message = "Excepcion al procesar transaccion: tiempo de respuesta superado."
    timestamp = "2026-11-16T10:00:00Z"
    source = "k8s-pod-payments-1"
    traceId = "trace-001"
    metadata = '{"requestId":"req-001","version":"1.0.0"}'
} | ConvertTo-Json -Compress

$log2 = @{
    serviceName = "inventory"
    level = "Information"
    message = "Operacion normal en inventory completada correctamente."
    timestamp = "2026-11-16T10:05:00Z"
    source = "k8s-pod-inventory-2"
    traceId = "trace-002"
    metadata = '{"requestId":"req-002","version":"1.0.0"}'
} | ConvertTo-Json -Compress

$log3 = @{
    serviceName = "payments"
    level = "Warning"
    message = "Cola de notificaciones por encima del umbral de advertencia."
    timestamp = "2026-11-16T10:10:00Z"
    source = "k8s-pod-payments-2"
    traceId = "trace-003"
    metadata = '{"requestId":"req-003","version":"1.0.0"}'
} | ConvertTo-Json -Compress

try {
    $r1 = Invoke-RestMethod -Uri "$baseUrl/api/logs" -Method Post -Body $log1 -ContentType "application/json"
    Write-Host "  Log 1 creado: $($r1.id)" -ForegroundColor Green
    $logId1 = $r1.id
} catch {
    Write-Host "  ERROR Log 1: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    $r2 = Invoke-RestMethod -Uri "$baseUrl/api/logs" -Method Post -Body $log2 -ContentType "application/json"
    Write-Host "  Log 2 creado: $($r2.id)" -ForegroundColor Green
    $logId2 = $r2.id
} catch {
    Write-Host "  ERROR Log 2: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    $r3 = Invoke-RestMethod -Uri "$baseUrl/api/logs" -Method Post -Body $log3 -ContentType "application/json"
    Write-Host "  Log 3 creado: $($r3.id)" -ForegroundColor Green
    $logId3 = $r3.id
} catch {
    Write-Host "  ERROR Log 3: $($_.Exception.Message)" -ForegroundColor Red
}

# --- 4. Listar logs ---
Write-Host ""
Write-Host "[4/8] Listando logs de payments..." -ForegroundColor Yellow
try {
    $list = Invoke-RestMethod -Uri "$baseUrl/api/logs?serviceName=payments&pageSize=5" -Method Get
    Write-Host "  Total: $($list.totalCount), Pagina: $($list.page)/$($list.totalPages)" -ForegroundColor Green
    $list.items | ForEach-Object { Write-Host "    - [$($_.level)] $($_.serviceName): $($_.message)" }
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# --- 5. Agregación ---
Write-Host ""
Write-Host "[5/8] Agregacion por servicio..." -ForegroundColor Yellow
try {
    $agg = Invoke-RestMethod -Uri "$baseUrl/api/logs/aggregation?groupBy=service" -Method Get
    $agg | ForEach-Object { Write-Host "    $($_.key): $($_.count)" -ForegroundColor Green }
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# --- 6. Análisis con IA ---
Write-Host ""
Write-Host "[6/8] Ejecutando analisis con IA..." -ForegroundColor Yellow
$analyzeBody = @{
    logIds = @($logId1, $logId2, $logId3)
} | ConvertTo-Json -Compress

try {
    $analysis = Invoke-RestMethod -Uri "$baseUrl/api/logs/analyze" -Method Post -Body $analyzeBody -ContentType "application/json"
    Write-Host "  Analisis creado: $($analysis.id)" -ForegroundColor Green
    Write-Host "  Titulo: $($analysis.title)" -ForegroundColor Green
    Write-Host "  Resumen: $($analysis.summary)" -ForegroundColor Green
    Write-Host "  Categorias: $($analysis.problemCategories -join ', ')" -ForegroundColor Green
    Write-Host "  Sugerencias: $($analysis.actionSuggestions -join '; ')" -ForegroundColor Green
    $analysisId = $analysis.id
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# --- 7. Consultar resultado persistido ---
Write-Host ""
Write-Host "[7/8] Consultando analisis persistido..." -ForegroundColor Yellow
try {
    $stored = Invoke-RestMethod -Uri "$baseUrl/api/analysis/$analysisId" -Method Get
    Write-Host "  ID: $($stored.id)" -ForegroundColor Green
    Write-Host "  Estado: $($stored.status)" -ForegroundColor Green
    Write-Host "  Logs analizados: $($stored.logCount)" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# --- 8. Verificar health ---
Write-Host ""
Write-Host "[8/8] Verificando health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -Method Get
    Write-Host "  Status: $($health.status), DB Reachable: $($health.databaseReachable)" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

# --- Resumen final ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Recorrido completo finalizado." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "URLs de prueba:"
Write-Host "  Swagger UI: $baseUrl" -ForegroundColor Cyan
Write-Host "  Health:     $baseUrl/api/health" -ForegroundColor Cyan
Write-Host "  Logs:       $baseUrl/api/logs" -ForegroundColor Cyan
Write-Host "  Logs by ID: $baseUrl/api/logs/$logId1" -ForegroundColor Cyan
Write-Host "  Aggregation: $baseUrl/api/logs/aggregation?groupBy=service" -ForegroundColor Cyan
Write-Host "  Analyze:    $baseUrl/api/logs/analyze" -ForegroundColor Cyan
Write-Host "  Analysis:   $baseUrl/api/analysis/$analysisId" -ForegroundColor Cyan
Write-Host ""
Write-Host "Para detener la aplicacion: Get-Process dotnet | Stop-Process -Force" -ForegroundColor Yellow
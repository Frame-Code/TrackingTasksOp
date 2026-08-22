# Registra Web.exe como servicio de Windows. Ejecutar como Administrador.
# ponytail: sc.exe directo, la app ya llama UseWindowsService() -> no hace falta NSSM ni wrapper.

$ErrorActionPreference = 'Stop'
$name = 'TrackingTask'
$exe  = Join-Path $PSScriptRoot 'Web.exe'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrator')) {
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`""
    return
}
if (-not (Test-Path $exe)) { throw "No existe $exe" }

if (Get-Service $name -ErrorAction SilentlyContinue) {
    Write-Host "El servicio $name ya existe; se recrea."
    sc.exe stop $name | Out-Null
    sc.exe delete $name | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $name binPath= "`"$exe`"" start= auto DisplayName= "Tracking Task Operations"
sc.exe description $name "API de seguimiento de tareas (ASP.NET Core / Kestrel). La URL se configura en appsettings.json."
# reinicio automatico ante caidas: 5s, 10s, 30s; contador se resetea cada 24h
sc.exe failure $name reset= 86400 actions= restart/5000/restart/10000/restart/30000

sc.exe start $name
Start-Sleep -Seconds 3
Get-Service $name | Format-List Name, Status, StartType

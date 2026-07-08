# killports.ps1
# Kills orphaned PlannerPro / Aspire processes left over from previous debug
# sessions that squat on the app ports and cause Kestrel to fail with:
#   SocketException: Only one usage of each socket address ... is normally permitted.
# Run this before launching if you hit that error.

$names = @('PlannerPro.AppHost', 'PlannerPro.Api', 'aspire', 'dcp')

$procs = Get-Process -Name $names -ErrorAction SilentlyContinue
if ($procs) {
    $procs | ForEach-Object { Write-Host "Stopping $($_.Name) (PID $($_.Id))" -ForegroundColor Yellow }
    $procs | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
} else {
    Write-Host "No orphaned PlannerPro/Aspire processes found." -ForegroundColor Green
}

$remaining = (Get-Process -Name $names -ErrorAction SilentlyContinue | Measure-Object).Count
if ($remaining -eq 0) {
    Write-Host "All clear - app ports are free. You can launch now." -ForegroundColor Green
} else {
    Write-Host "$remaining process(es) still running - try again or check Task Manager." -ForegroundColor Red
}

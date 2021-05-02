[System.IO.File]::ReadLines((Resolve-Path ".\SetEnvVars.bat").Path) | ForEach-Object {
       $ar = $_.Split(" =")
       $key=$ar[1]
       $val=$ar[2]
       if ($key -and $val) {
              Write-Output "Setting '$key' to '$val'"
              [Environment]::SetEnvironmentVariable($ar[1],$ar[2])
       }
}
net start WinRM
Set-Item WSMan:\localhost\Client\TrustedHosts -Value rvm -force
Enter-PsSession -ComputerName rvm -Credential rvm\Administrator #pass is "CorrectHorseBatteryStaple"
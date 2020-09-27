#run powershell as admin

# more info on running powershell scripts https://www.windowscentral.com/how-create-and-run-your-first-powershell-script-file-windows-10
# more info on service users https://social.msdn.microsoft.com/forums/sqlserver/en-US/31d57870-1faa-4e14-8527-ce77b1ff40e4/local-service-local-system-or-network-service
# more info on service user account: https://docs.microsoft.com/en-us/windows/win32/services/networkservice-account

#INSTRUCTIONS: 
# 1. run these commands, cancelling login dialog when it appears. Can copy/paste into powershell, or double click this file.
# 2. confirm it is running in Services applet (lower left hand magnifying glass, type 'services', click 'services app')
# 3. troubleshoot in wondows event viewer (lower left hand magnifying glass, type 'event', click 'event viewer', select 'windows logs', then 'application')


$appdir = "C:\DentalEMR\DemrService"
$appfullpath ="C:\DentalEMR\DemrService\DemrService.exe"
$description = "Dental EMR Bridge"
$displayname = "Dental EMR Bridge"

$servicename = "DemrService"
$user = "NT AUTHORITY\SYSTEM"
$acl = Get-Acl $appdir
$aclRuleArgs =  $user, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($aclRuleArgs)
$acl.SetAccessRule($accessRule)
$acl | Set-Acl $appfullpath

New-Service -Name $servicename -BinaryPathName $appfullpath -Credential $user -Description $description -DisplayName $displayname -StartupType Automatic
sc.exe start $servicename 



# sc.exe delete $servicename
# sc.exe stop $servicename

# Fixed Hostinger transport probe: no AUTH, MAIL FROM, RCPT TO or message body.
$ErrorActionPreference='Stop'
$client=$null; $stream=$null; $reader=$null; $writer=$null; $tls=$null
$report=[ordered]@{Host='smtp.hostinger.com';Port=587;StartTlsAdvertised=$false;TlsNegotiated=$false;CertificateValidationBypassed=$false;Authenticated=$false;MessageSent=$false;CheckedAtUtc=[DateTime]::UtcNow.ToString('O');FailureType=$null}
function Read-SmtpReply($smtpReader,[int]$expectedCode){
 for($lineCount=0;$lineCount -lt 100;$lineCount++){
  $reply=$smtpReader.ReadLine()
  if($null -eq $reply -or $reply.Length -lt 4 -or $reply.Length -gt 4096){throw 'Invalid SMTP reply.'}
  if($reply.Substring(0,3) -ne [string]$expectedCode){throw 'Unexpected SMTP reply code.'}
  if($reply.Substring(4).Trim() -eq 'STARTTLS'){$script:report.StartTlsAdvertised=$true}
  if($reply[3] -eq ' '){return}
  if($reply[3] -ne '-'){throw 'Invalid SMTP continuation.'}
 }
 throw 'SMTP reply limit exceeded.'
}
try{
 $client=[Net.Sockets.TcpClient]::new()
 $connect=$client.ConnectAsync('smtp.hostinger.com',587)
 if(-not $connect.Wait(10000)){throw 'SMTP connect timeout.'}
 $stream=$client.GetStream(); $stream.ReadTimeout=10000; $stream.WriteTimeout=10000
 $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::ASCII,$false,1024,$true)
 $writer=[IO.StreamWriter]::new($stream,[Text.Encoding]::ASCII,1024,$true);$writer.NewLine="`r`n";$writer.AutoFlush=$true
 Read-SmtpReply $reader 220
 $writer.WriteLine('EHLO app.neostp.com');Read-SmtpReply $reader 250
 if(-not $report.StartTlsAdvertised){throw 'STARTTLS required.'}
 $writer.WriteLine('STARTTLS');Read-SmtpReply $reader 220
 $reader.Dispose();$reader=$null;$writer.Dispose();$writer=$null
 # No custom certificate callback: platform hostname/chain validation remains active.
 $tls=[Net.Security.SslStream]::new($stream,$true)
 $tls.ReadTimeout=10000;$tls.WriteTimeout=10000
 $tls.AuthenticateAsClient('smtp.hostinger.com')
 $report.TlsNegotiated=$tls.IsAuthenticated -and $tls.IsEncrypted
 $report.Protocol=$tls.SslProtocol.ToString()
}catch{$report.FailureType=$_.Exception.GetType().Name}
finally{if($reader){$reader.Dispose()};if($writer){$writer.Dispose()};if($tls){$tls.Dispose()};if($stream){$stream.Dispose()};if($client){$client.Dispose()}}
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputDir=Join-Path $repoPath 'tmp/client-certification-2026-09-05'
[void][IO.Directory]::CreateDirectory($outputDir)
$report | ConvertTo-Json | Set-Content (Join-Path $outputDir 'smtp-transport.json') -Encoding utf8
$report | ConvertTo-Json
if(-not $report.TlsNegotiated){exit 1}
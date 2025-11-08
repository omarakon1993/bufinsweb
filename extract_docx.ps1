Add-Type -AssemblyName System.IO.Compression.FileSystem
$docxPath = "C:\Users\omara\OneDrive\Omar Akon Personal\Proyectos OAG\Bufins Web\Common\Nueva web bufins informativa - 2025\Textos nuevos Bufins - Resumido nuevo.docx"
$zip = [System.IO.Compression.ZipFile]::OpenRead($docxPath)
$entry = $zip.Entries | Where-Object { $_.Name -eq 'document.xml' }
$stream = $entry.Open()
$reader = New-Object System.IO.StreamReader($stream)
$content = $reader.ReadToEnd()
$reader.Close()
$zip.Dispose()
$content

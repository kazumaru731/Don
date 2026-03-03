$folders = @{ 'Spade'=0; 'Club'=13; 'Diamond'=26; 'Heart'=39 }
foreach ($suit in $folders.Keys) {
    $offset = $folders[$suit]
    for ($i = 1; $i -le 13; $i++) {
        $oldNum = $offset + $i
        $oldPath = "d:\Unity_projects\Don\Assets\Cards\$suit\torannpu-illust$oldNum.png"
        $newPath = "d:\Unity_projects\Don\Assets\Cards\$suit\torannpu-illust$i.png"
        $oldMeta = "$oldPath.meta"
        $newMeta = "$newPath.meta"
        
        # Don't try to rename Spade files since they are already 1-13
        if ($oldNum -ne $i) {
            if (Test-Path $oldPath) { Move-Item -Path $oldPath -Destination $newPath -Force }
            if (Test-Path $oldMeta) { Move-Item -Path $oldMeta -Destination $newMeta -Force }
        }
    }
}
Write-Output "Renaming Complete"

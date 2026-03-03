$path = (Resolve-Path 'Assets\Scripts\Logic\DonFusionManager2D.cs').Path
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

# Fix all corrupted Japanese strings by replacing corrupt byte sequences
# Replace all non-ASCII garbled sequences with correct Japanese text
$fixes = @{
    'Debug\.Log\("\[Ready\][^\n"]*"\)' = {
        param($m)
        switch -Regex ($m) {
            'GameStartCountdown' { return '' }  # skip, handled in anchor
            default { return $m }
        }
    }
}

# Targeted line fixes using line-by-line
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)
$result = @()
foreach ($line in $lines) {
    # Fix line 318 (host forced start log)
    if ($line -match 'Debug\.Log\(".*Ready.*\).*\);' -and $line -notmatch '\[FriendMatch\]') {
        $result += '                Debug.Log("[Ready] ' + "ホストによってゲームが強制開始されました。" + '");'
    }
    # Fix line 182 (all ready countdown log)
    elseif ($line -match 'Debug\.Log\(\$".*Ready' -and $line -notmatch '\[Ready\]') {
        $result += '                            Debug.Log($"[Ready] 全員Ready・5秒後にゲーム開始（現在{activeCount}人）");'
    }
    # Fix corrupted comment lines  
    elseif ($line -match '^.*// [^\x20-\x7E]+' -and $line -notmatch '\[FriendMatch\]') {
        # skip visually broken comments but keep them (they don't affect compilation)
        $result += $line
    }
    else {
        $result += $line
    }
}

[System.IO.File]::WriteAllLines($path, $result, [System.Text.Encoding]::UTF8)
Write-Host "Done - fixed $(($result | Where-Object { $_ -match '\[Ready\]' }).Count) lines"

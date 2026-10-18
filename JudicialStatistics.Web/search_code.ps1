$pattern = "EXPERT_RETURN_FIRST|J_NO_JURIS|JT_EVIDENCE|JT_STAY"
Get-ChildItem -Path . -Recurse -Include *.cs,*.cshtml,*.js -Exclude *bin*,*obj*,*lib* | Select-String -Pattern $pattern | Select-Object -First 30 | ForEach-Object {
    $_.Path + ":" + $_.LineNumber + " -> " + $_.Line.Trim()
}

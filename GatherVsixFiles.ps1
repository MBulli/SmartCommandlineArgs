$tag = git describe --tags

if ($LastExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($tag)) {
    $filesMap = @{
        "SmartCmdArgs\SmartCmdArgs15\bin\Release\SmartCmdArgs15.vsix" = "SmartCmdArgs-vs2017-{0}.vsix";
        "SmartCmdArgs\SmartCmdArgs16\bin\Release\SmartCmdArgs16.vsix" = "SmartCmdArgs-vs2019-{0}.vsix";
        "SmartCmdArgs\SmartCmdArgs17\bin\Release\SmartCmdArgs17.vsix" = "SmartCmdArgs-vs2022-{0}.vsix";
    }

    foreach ($file in $filesMap.Keys) {
        $newFileName = $filesMap[$file] -f $tag
        $destinationPath = ".\$newFileName"
        Copy-Item $file -Destination $destinationPath

        if ($LastExitCode -eq 0) {
            Write-Host "Copied and renamed $file to $newFileName successfully."
        } else {
            Write-Host "Failed to copy $file."
        }
    }
} else {
    Write-Host "Failed to get the latest git tag."
}
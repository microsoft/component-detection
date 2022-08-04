$testTreeRootName = [System.IO.Path]::Combine([System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "test-tree"), [System.Guid]::NewGuid())

if (Test-Path $testTreeRootName) {
    Remove-Item $testTreeRootName
}

function CreateValidFileTree {
    param (
        [string]$rootPath
    )
    Push-Location -Path $rootPath
    New-Item -ItemType Directory -Path ./a/b/c/d/e/f
    Out-File -FilePath ./a/a.txt
    Out-File -FilePath ./a/b/b.txt
    Out-File -FilePath ./a/b/c/c.txt
    Out-File -FilePath ./a/b/c/d/d.txt
    Out-File -FilePath ./a/b/c/d/e/e.txt
    Out-File -FilePath ./a/b/c/d/e/f/f.txt
    Pop-Location
}

New-item -ItemType Directory -Path $testTreeRootName

Push-Location -Path $testTreeRootName

New-Item -ItemType Directory -Path ./root/cycle
CreateValidFileTree ./root/cycle
New-Item -ItemType SymbolicLink -Path ./root/cycle/a/b/c/d/link-to-b -Target ./root/cycle/a/b

New-Item -ItemType Directory -Path ./root/recursive-links
CreateValidFileTree ./root/recursive-links
New-Item -ItemType Directory -Path ./root/recursive-links/link-to-a
New-Item -ItemType SymbolicLink -Path ./root/recursive-links/link-to-b -Target './root/recursive-links/link-to-a'
Remove-Item ./root/recursive-links/link-to-a
New-Item -ItemType SymbolicLink -Path ./root/recursive-links/link-to-a -Target './root/recursive-links/link-to-b'

New-Item -ItemType Directory -Path ./outside-root
CreateValidFileTree ./outside-root

New-Item -ItemType Directory -Path ./root/valid-links
CreateValidFileTree ./root/valid-links
New-Item -ItemType SymbolicLink -Path ./root/valid-links/first-link-to-b -Target ./root/valid-links/a/b
New-Item -ItemType SymbolicLink -Path ./root/valid-links/second-link-to-b -Target ./root/valid-links/a/b
New-Item -ItemType SymbolicLink -Path ./root/valid-links/first-link-to-outside -Target ./outside-root/a/b
New-Item -ItemType SymbolicLink -Path ./root/valid-links/second-link-to-outside -Target ./outside-root

New-Item -ItemType SymbolicLink -Path ./outside-root/link-to-root-parent -Target ./
New-Item -ItemType SymbolicLink -Path ./outside-root/link-to-root -Target ./root

if ([System.Environment]::OSVersion.Platform -eq "Win32NT") {
    New-Item -ItemType Directory -Path ./outside-root-two
    CreateValidFileTree ./outside-root-two

    New-Item -ItemType Directory -Path ./root/junctions
    New-Item -ItemType Junction -Path ./root/junctions/unknown-files-junction -Target ./outside-root-two
    New-Item -ItemType Junction -Path ./root/junctions/known-files-junction -Target ./outside-root

    New-Item -ItemType Directory -Path ./outside-junction-cycles-a
    CreateValidFileTree ./outside-junction-cycles-a

    New-Item -ItemType Directory -Path ./outside-junction-cycles-b
    CreateValidFileTree ./outside-junction-cycles-b

    New-Item -ItemType Directory -Path ./root/junction-cycles
    CreateValidFileTree ./root/junction-cycles

    New-Item -ItemType Junction -Path ./root/junction-cycles/a/b/c/d/e/f/junction-to-outside-q -Target ./outside-junction-cycles-a
    New-Item -ItemType Junction -Path ./root/junction-cycles/a/b/c/d/e/f/junction-to-outside-r -Target ./outside-junction-cycles-b
    New-Item -ItemType Junction -Path ./root/junction-cycles/a/b/c/d/e/f/junction-to-outside-s -Target ./outside-junction-cycles-a
    New-Item -ItemType Junction -Path ./root/junction-cycles/a/b/c/d/e/f/junction-to-outside-t -Target ./outside-junction-cycles-b
    New-Item -ItemType Junction -Path ./outside-junction-cycles-a/junction-to-outside-b -Target ./outside-junction-cycles-b
    New-Item -ItemType Junction -Path ./outside-junction-cycles-b/junction-to-outside-a -Target ./outside-junction-cycles-a/a/b
    New-Item -ItemType Junction -Path ./outside-junction-cycles-b/junction-to-outside-a-b -Target ./outside-junction-cycles-a/junction-to-outside-b
}

Write-Host "##vso[task.setvariable variable=COMPONENT_DETECTION_SYMLINK_TEST]$testTreeRootName"
Pop-Location

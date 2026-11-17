param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [switch]$Fix
)

$ErrorActionPreference = 'Stop'

$directive = '^(#!|#requires\b|#\s*zizmor:\s*ignore\[)'

function Get-CSharpComments([string]$text) {
    if (-not ('Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree' -as [type])) {
        Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
    }
    $options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Preview)
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text, $options)
    $kinds = @('SingleLineCommentTrivia', 'MultiLineCommentTrivia', 'SingleLineDocumentationCommentTrivia', 'MultiLineDocumentationCommentTrivia')
    foreach ($trivia in $tree.GetRoot().DescendantTrivia($null, $true)) {
        if ([Microsoft.CodeAnalysis.CSharp.CSharpExtensions]::Kind($trivia).ToString() -in $kinds) {
            $end = $trivia.FullSpan.End
            while ($end -gt $trivia.FullSpan.Start -and $text[$end - 1] -match '[\r\n]') { $end-- }
            [pscustomobject]@{ Start = $trivia.FullSpan.Start; End = $end }
        }
    }
}

function Get-PowerShellComments([string]$text) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseInput($text, [ref]$tokens, [ref]$errors)
    foreach ($token in $tokens) {
        if ($token.Kind -eq 'Comment' -and $token.Text -notmatch $directive) {
            [pscustomobject]@{ Start = $token.Extent.StartOffset; End = $token.Extent.EndOffset }
        }
    }
}

function Get-XmlComments([string]$text) {
    foreach ($match in [regex]::Matches($text, '<!--.*?-->', 'Singleline')) {
        [pscustomobject]@{ Start = $match.Index; End = ($match.Index + $match.Length) }
    }
}

function Get-HashComments([string]$text, [bool]$trailing) {
    $offset = 0
    foreach ($line in ($text -split '\n')) {
        $quote = $null
        for ($i = 0; $i -lt $line.Length; $i++) {
            $c = $line[$i]
            if ($quote) {
                if ($c -eq $quote) { $quote = $null }
            } elseif ($trailing -and ($c -eq '"' -or $c -eq "'") -and ($i -eq 0 -or $line[$i - 1] -match '[\s:\[{,(=]')) {
                $quote = $c
            } elseif ($c -eq '#' -and ($line.Substring(0, $i).Trim() -eq '' -or ($trailing -and $line[$i - 1] -match '\s'))) {
                $comment = $line.Substring($i).TrimEnd("`r")
                if ($comment -notmatch $directive) {
                    [pscustomobject]@{ Start = ($offset + $i); End = ($offset + $i + $comment.Length) }
                }
                break
            }
        }
        $offset += $line.Length + 1
    }
}

function Get-Comments([string]$path, [string]$text) {
    switch -Regex ($path) {
        '\.cs$' { Get-CSharpComments $text; break }
        '\.ps1$' { Get-PowerShellComments $text; break }
        '\.(csproj|props|targets|slnx|manifest|config|xml)$' { Get-XmlComments $text; break }
        '\.ya?ml$' { Get-HashComments $text $true; break }
        '(^|/)\.(editorconfig|gitignore|gitattributes)$' { Get-HashComments $text $false; break }
    }
}

function Test-Blank([string]$text, [int]$start, [int]$end) {
    return $text.Substring($start, $end - $start).Trim() -eq ''
}

function Remove-Comment([string]$text, [int]$start, [int]$end) {
    $lineStart = $text.LastIndexOf("`n", [Math]::Max($start - 1, 0)) + 1
    if ($start -eq 0) { $lineStart = 0 }
    $lineEnd = $text.IndexOf("`n", $end)
    if ($lineEnd -lt 0) { $lineEnd = $text.Length }

    if (-not (Test-Blank $text $end $lineEnd)) {
        $stop = $end
        if ($start -gt 0 -and $text[$start - 1] -eq ' ' -and $text[$stop] -eq ' ') { $stop++ }
        return $text.Remove($start, $stop - $start)
    }
    if (-not (Test-Blank $text $lineStart $start)) {
        $from = $start
        while ($from -gt $lineStart -and $text[$from - 1] -match '[ \t]') { $from-- }
        $to = $lineEnd
        if ($to -gt 0 -and $text[$to - 1] -eq "`r") { $to-- }
        return $text.Remove($from, $to - $from)
    }

    $text = $text.Remove($lineStart, [Math]::Min($lineEnd + 1, $text.Length) - $lineStart)
    $nextEnd = $text.IndexOf("`n", $lineStart)
    if ($nextEnd -lt 0) { $nextEnd = $text.Length }
    $next = $text.Substring($lineStart, $nextEnd - $lineStart).Trim()
    $nextBlank = $nextEnd -lt $text.Length -and $next -eq ''
    if ($lineStart -eq 0) {
        if ($nextBlank) { $text = $text.Remove(0, $nextEnd + 1) }
        return $text
    }
    $prevStart = $text.LastIndexOf("`n", [Math]::Max($lineStart - 2, 0)) + 1
    if ($lineStart -lt 2) { $prevStart = 0 }
    $prev = $text.Substring($prevStart, $lineStart - $prevStart).Trim()
    if ($nextBlank -and ($prev -eq '' -or $prev.EndsWith('{'))) {
        $text = $text.Remove($lineStart, $nextEnd + 1 - $lineStart)
    } elseif ($prev -eq '' -and ($next.StartsWith('}') -or $nextEnd -eq $text.Length)) {
        $text = $text.Remove($prevStart, $lineStart - $prevStart)
    }
    return $text
}

$tracked = @(& git -c core.quotepath=off -C $Root ls-files)
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }

$found = 0
$files = 0
foreach ($path in $tracked) {
    $full = Join-Path $Root $path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($full)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.Text.UTF8Encoding]::new($false).GetString($bytes, ($bom ? 3 : 0), $bytes.Length - ($bom ? 3 : 0))
    $spans = @(Get-Comments $path $text)
    if ($spans.Count -eq 0) { continue }
    $files++
    $found += $spans.Count

    if (-not $Fix) {
        foreach ($span in $spans) {
            $line = ($text.Substring(0, $span.Start) -split '\n').Count
            $first = ($text.Substring($span.Start, $span.End - $span.Start) -split '\r?\n')[0]
            Write-Output "Comment: ${path}:${line}: $first"
        }
        continue
    }
    foreach ($span in ($spans | Sort-Object Start -Descending)) {
        $text = Remove-Comment $text $span.Start $span.End
    }
    [System.IO.File]::WriteAllText($full, $text, [System.Text.UTF8Encoding]::new($bom))
}

if ($Fix) {
    Write-Output "removed $found comments from $files files"
    exit 0
}
if ($found -gt 0) {
    Write-Output "$found comments in $files files. Delete them, or run scripts/comments.ps1 -Fix."
    exit 1
}
Write-Output "no comments in $($tracked.Count) tracked files"
exit 0

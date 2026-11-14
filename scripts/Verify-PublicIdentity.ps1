$ErrorActionPreference = 'Stop'
$rows = & git log --all --format='%H%x09%an%x09%ae%x09%cn%x09%ce'
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect commit identities' }
foreach ($row in $rows) {
    $fields = $row -split "`t"
    foreach ($index in @(2,4)) {
        if ($fields[$index] -notmatch '(^noreply@github\.com$|@users\.noreply\.github\.com$)') {
            throw "Commit $($fields[0]) uses a non-noreply identity. No address has been written to the log."
        }
    }
}
Write-Host 'Commit author and committer emails use GitHub noreply identities.'

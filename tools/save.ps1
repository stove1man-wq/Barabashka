# Коммит и отправка текущей ветки одной командой.
# Пример: .\tools\save.ps1 "fix(media): описание"
param([Parameter(Mandatory = $true)][string]$Message)
$ErrorActionPreference = "Stop"
$branch = git branch --show-current
git status -sb
git add -A
git commit -m $Message
git pull --rebase origin $branch 2>$null
git push -u origin $branch

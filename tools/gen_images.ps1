# 잭팟 도감 이미지 무료 자동 생성 (pollinations.ai) — prompts.json → img/<id>.png
$root = "C:\dev\JackpotRunWeb"
$img = "$root\public\jackpotdex\img"
New-Item -ItemType Directory -Force $img | Out-Null
$data = Get-Content "$root\tools\prompts.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$suffix = $data."_suffix"
$ids = $data.PSObject.Properties.Name | Where-Object { $_ -ne "_suffix" }
$ok = 0; $skip = 0; $fail = 0
foreach ($id in $ids) {
  $out = "$img\$id.png"
  if ((Test-Path $out) -and (Get-Item $out).Length -gt 2000) { $skip++; continue }
  $prompt = $data.$id + $suffix
  $enc = [uri]::EscapeDataString($prompt)
  $seed = [Math]::Abs($id.GetHashCode()) % 100000
  $url = "https://image.pollinations.ai/prompt/$enc`?width=256&height=256&nologo=true&seed=$seed"
  try {
    Invoke-WebRequest -Uri $url -OutFile $out -TimeoutSec 120
    if ((Get-Item $out).Length -gt 2000) { $ok++; Write-Output "OK $id ($((Get-Item $out).Length)b)" }
    else { $fail++; Write-Output "SMALL $id" }
  } catch { $fail++; Write-Output "FAIL $id : $($_.Exception.Message)" }
}
Write-Output "=== done: ok=$ok skip=$skip fail=$fail / total=$($ids.Count) ==="

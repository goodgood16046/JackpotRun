# 잭팟런 — 이미지 없는 장치 4종 생성 (pollinations.ai)
# ⚠️ 실행하면 외부 서비스(pollinations.ai)로 프롬프트가 전송됩니다. 자동 실행되지 않았습니다.
#
#   .\regen_missing.ps1              # 기존과 동일한 256x256
#   .\regen_missing.ps1 -Size 1024   # Unity용 고해상도
param([int]$Size = 256)

$img = Join-Path $PSScriptRoot "Sprites\Devices"
New-Item -ItemType Directory -Force $img | Out-Null

# 기존 dev_* 12종과 동일한 화풍으로 맞춘 프롬프트
$suffix = ", single subject centered, soft plain dark studio gradient background, highly detailed digital painting, game collectible card art, vibrant colors, sharp focus"
$prompts = @{
  "dev_holdfile" = "a labeled folder file holder keeping one glowing card tile in reserve, gadget device"
  "dev_major"    = "an official major declaration form with a graduation cap seal and glowing checkmark, gadget device"
  "dev_retake"   = "a retake exam ticket with a circular redraw arrow and scattered coins, gadget device"
  "dev_syllabus" = "a clipboard syllabus revealing preview star ratings of upcoming cards, gadget device"
}

$ok = 0; $fail = 0
foreach ($id in $prompts.Keys) {
  $out = Join-Path $img "$id.png"
  if ((Test-Path $out) -and (Get-Item $out).Length -gt 2000) { Write-Output "SKIP $id"; continue }
  $enc = [uri]::EscapeDataString($prompts[$id] + $suffix)
  $seed = [Math]::Abs($id.GetHashCode()) % 100000
  $url = "https://image.pollinations.ai/prompt/$enc`?width=$Size&height=$Size&nologo=true&seed=$seed"
  try {
    Invoke-WebRequest -Uri $url -OutFile $out -TimeoutSec 120
    if ((Get-Item $out).Length -gt 2000) { $ok++; Write-Output "OK $id ($((Get-Item $out).Length)b)" }
    else { $fail++; Write-Output "SMALL $id" }
  } catch { $fail++; Write-Output "FAIL $id : $($_.Exception.Message)" }
}
Write-Output "=== done: ok=$ok fail=$fail / total=$($prompts.Count) @ ${Size}px ==="

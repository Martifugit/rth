# The small window that shows while the Codex updates (electron/update-splash.js), as Discord's does.
# The installer replaces the Codex's own files, so the Codex can't show anything itself while it runs:
# this runs on its own (Windows' PowerShell and WinForms), from a temporary folder the Codex copied it
# to with the logo, the stone and Cinzel. It writes "shown" once it's on screen (the Codex then closes
# and the installer starts), and closes when the new Codex writes "done", or after a few minutes.
param([string]$Dir, [string]$Version)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$gold = [System.Drawing.Color]::FromArgb(0xE0, 0xC3, 0x87)
$muted = [System.Drawing.Color]::FromArgb(0xA8, 0x9C, 0x86)
$edge = [System.Drawing.Color]::FromArgb(0x2C, 0x25, 0x1C)

# Cinzel, as in the Codex; Georgia if it can't be loaded.
$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$serif = "Georgia"
try { $fonts.AddFontFile((Join-Path $Dir "Cinzel.ttf")); $serif = $fonts.Families[0] } catch {}
function Serif($size) { New-Object System.Drawing.Font($serif, $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel) }

$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = "None"
$form.StartPosition = "CenterScreen"
$form.Size = New-Object System.Drawing.Size(340, 230)
$form.BackColor = [System.Drawing.Color]::FromArgb(0x0D, 0x0C, 0x0A)
$form.ShowInTaskbar = $true
$form.TopMost = $true
$form.Text = "Updating R'th Codex"
try { $form.Icon = New-Object System.Drawing.Icon((Join-Path $Dir "logo.ico")) } catch {}
try { $form.BackgroundImage = [System.Drawing.Image]::FromFile((Join-Path $Dir "stone.jpg")); $form.BackgroundImageLayout = "Tile" } catch {}

# Gold bar sliding along its track (indeterminate: the installer says nothing of how far it is).
$script:pos = -90
$form.Add_Paint({
  param($s, $e)
  $g = $e.Graphics
  $g.SmoothingMode = "AntiAlias"
  $g.DrawRectangle((New-Object System.Drawing.Pen($edge)), 0, 0, $form.Width - 1, $form.Height - 1)
  $track = New-Object System.Drawing.Rectangle(40, 190, ($form.Width - 80), 6)
  $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0x1D, 0x19, 0x12))), $track)
  $g.SetClip($track)
  $bar = New-Object System.Drawing.Rectangle(($track.X + $script:pos), $track.Y, 90, $track.Height)
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bar, [System.Drawing.Color]::FromArgb(0xD9, 0xB3, 0x6A), [System.Drawing.Color]::FromArgb(0xA8, 0x80, 0x3F), 90)
  $g.FillRectangle($brush, $bar)
  $g.ResetClip()
  $g.DrawRectangle((New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0x3A, 0x31, 0x25))), $track)
})
$form.GetType().GetProperty("DoubleBuffered", [System.Reflection.BindingFlags]"Instance,NonPublic").SetValue($form, $true)

# Centred across the window.
function Text($text, $font, $color, $y, $h) {
  $l = New-Object System.Windows.Forms.Label
  $l.Text = $text; $l.Font = $font; $l.ForeColor = $color; $l.BackColor = [System.Drawing.Color]::Transparent
  $l.TextAlign = "MiddleCenter"
  $l.Location = New-Object System.Drawing.Point(0, $y); $l.Size = New-Object System.Drawing.Size($form.Width, $h)
  $form.Controls.Add($l); $l
}
$logo = New-Object System.Windows.Forms.PictureBox
$logo.Location = New-Object System.Drawing.Point((($form.Width - 64) / 2), 28); $logo.Size = New-Object System.Drawing.Size(64, 64)
$logo.SizeMode = "Zoom"; $logo.BackColor = [System.Drawing.Color]::Transparent
try { $logo.Image = [System.Drawing.Image]::FromFile((Join-Path $Dir "logo.png")) } catch {}
$form.Controls.Add($logo)
$heading = if ($Version) { "Updating to $Version" } else { "Updating" }
[void](Text $heading (Serif 20) $gold 106 30)
$status = Text "Codex opens again in a moment." (New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.GraphicsUnit]::Pixel)) $muted 140 22

$started = Get-Date
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 30
$timer.Add_Tick({
  $script:pos += 4
  if ($script:pos -gt ($form.Width - 80)) { $script:pos = -90 }
  $form.Invalidate()
  $age = ((Get-Date) - $started).TotalSeconds
  # The new Codex is on screen (and we've been seen for long enough not to flicker): fade out.
  if ($age -gt 1.5 -and (Test-Path (Join-Path $Dir "done"))) {
    $form.Opacity -= 0.12
    if ($form.Opacity -le 0.05) { $timer.Stop(); $form.Close() }
  }
  elseif ($age -gt 240) {
    # Something went wrong: say how to carry on, then go.
    $status.Text = "Open Codex from the Start menu."
    if ($age -gt 255) { $timer.Stop(); $form.Close() }
  }
  elseif ($age -gt 90) { $status.Text = "This is taking longer than it should..." }
})
$form.Add_Shown({
  Set-Content -Path (Join-Path $Dir "shown") -Value "shown"
  $timer.Start()
})
# Started detached (no console), Windows makes the first window a process shows hidden: spend that
# first show on nothing, so the real one below is seen.
$form.Opacity = 0; $form.Show(); $form.Hide(); $form.Opacity = 1
# Click and drag anywhere to move it.
$form.Add_MouseDown({ $script:drag = [System.Windows.Forms.Cursor]::Position; $script:at = $form.Location })
$form.Add_MouseMove({ if ($_.Button -eq "Left") { $p = [System.Windows.Forms.Cursor]::Position; $form.Location = New-Object System.Drawing.Point(($script:at.X + $p.X - $script:drag.X), ($script:at.Y + $p.Y - $script:drag.Y)) } })

[System.Windows.Forms.Application]::Run($form)

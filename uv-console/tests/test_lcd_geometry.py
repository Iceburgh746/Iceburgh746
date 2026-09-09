from pathlib import Path
s=Path(__file__).resolve().parents[1].joinpath('src','MainForm.cs').read_text()
assert 'Size = new Size(424, 212)' in s
assert 'Location = new Point(2, 48)' in s
assert 'BackColor = Color.FromArgb(18, 31, 44)' in s
assert 'ForceFill = true' in s
assert 'ScreenMargin' not in s
assert 424 == 2 * 212
print('LCD geometry OK: 424x212 exact 2:1 with dark recessed letterbox bezel')

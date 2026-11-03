import base64, segno
for line in open('out.txt', encoding='utf-8'):
    if not line.startswith('MAT '): continue
    parts = line.strip().split(' ')
    idx, mask, b64, rows = parts[1], int(parts[2].split('=')[1]), parts[3], parts[4]
    data = base64.b64decode(b64)
    mine = [r for r in rows.split('|') if r]
    n = len(mine); ver = (n - 17) // 4
    q = segno.make_qr(data, error='m', version=ver, mask=mask, boost_error=False, mode='byte')
    ref = [''.join('1' if c else '0' for c in row) for row in q.matrix]
    diff = [(y, x) for y in range(n) for x in range(n) if mine[y][x] != ref[y][x]]
    print(f"CMP q{idx} v{ver} mask{mask} diffs={len(diff)}" + (f" first={diff[:8]}" if diff else ""), flush=True)

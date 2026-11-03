import cv2, glob
d = cv2.QRCodeDetector()
ok = bad = 0
for png in sorted(glob.glob('qr/*.png')):
    want = open(png[:-4] + '.txt', encoding='utf-8').read()
    img = cv2.copyMakeBorder(cv2.imread(png), 20, 20, 20, 20, cv2.BORDER_CONSTANT, value=(255, 255, 255))
    got, pts, _ = d.detectAndDecode(img)
    if got == want: ok += 1
    else: bad += 1
    print("DECODE", png, "OK" if got == want else "MISMATCH", len(want), len(got), flush=True)
print("DECODE passed", ok, "failed", bad, flush=True)

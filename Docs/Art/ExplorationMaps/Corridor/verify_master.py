"""Verify that Aseprite's layered master composites to the same art."""

from pathlib import Path
import time
from PIL import Image


root = Path(__file__).resolve().parent
roundtrip = root / "Preview/master-roundtrip.png"
expected = root / "Preview/corridor-composite.png"
for attempt in range(100):
    if roundtrip.is_file() and roundtrip.stat().st_size > 0:
        try:
            with Image.open(roundtrip) as probe:
                probe.verify()
            break
        except OSError:
            pass
    time.sleep(0.1)
else:
    raise RuntimeError("Aseprite roundtrip export was not written")
with Image.open(roundtrip) as src, Image.open(expected) as ref:
    a = src.convert("RGBA")
    b = ref.convert("RGBA")
    assert a.size == b.size == (4096, 724)
    assert a.tobytes() == b.tobytes(), "Aseprite and PNG composite differ"
roundtrip.unlink()
with (root / "qa-report.txt").open("a", encoding="utf-8") as report:
    report.write("PASS: Aseprite master roundtrip is pixel-identical to authored composite.\n")
print("PASS: Aseprite master roundtrip is pixel-identical to authored composite.")

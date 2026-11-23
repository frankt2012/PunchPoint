#!/usr/bin/env python3
"""Generates the PunchPoint application icon (no external dependencies).

Draws a rounded blue tile with a white clock, at 4x supersampling, then writes a
multi-resolution .ico containing PNG-encoded frames.
"""
import math
import os
import struct
import zlib

OUT = os.path.join(os.path.dirname(__file__), "..", "src", "PunchPoint.App", "Resources", "punchpoint.ico")

BLUE = (37, 99, 235)
BLUE_DARK = (29, 78, 216)
WHITE = (255, 255, 255)


def rounded_rect_sdf(x, y, cx, cy, half_w, half_h, radius):
    dx = abs(x - cx) - (half_w - radius)
    dy = abs(y - cy) - (half_h - radius)
    outside = math.hypot(max(dx, 0.0), max(dy, 0.0))
    inside = min(max(dx, dy), 0.0)
    return outside + inside - radius


def segment_distance(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    denom = vx * vx + vy * vy
    t = 0.0 if denom == 0 else max(0.0, min(1.0, (wx * vx + wy * vy) / denom))
    cx, cy = ax + t * vx, ay + t * vy
    return math.hypot(px - cx, py - cy)


def render(size, ss=4):
    n = size * ss
    c = n / 2.0
    half = n * 0.46
    radius = n * 0.22

    # Clock geometry, relative to the tile.
    ring_r = n * 0.285
    ring_w = n * 0.055
    hand_w = n * 0.048
    hour_len = n * 0.165
    minute_len = n * 0.225

    # Hands: 10:10 reads clearly as a clock at small sizes.
    minute_angle = math.radians(-90 + 60)   # 10 minutes past
    hour_angle = math.radians(-90 + 60 * 2)  # 2 o'clock-ish for balance

    hx = c + math.cos(hour_angle) * hour_len
    hy = c + math.sin(hour_angle) * hour_len
    mx = c + math.cos(minute_angle) * minute_len
    my = c + math.sin(minute_angle) * minute_len

    # Pre-computed coverage buffer at supersampled resolution.
    rows = []
    for y in range(n):
        row = bytearray()
        for x in range(n):
            px, py = x + 0.5, y + 0.5
            d = rounded_rect_sdf(px, py, c, c, half, half, radius)
            if d > 0.5:
                row += bytes((0, 0, 0, 0))
                continue

            # Base gradient-ish fill.
            t = (py / n)
            r = int(BLUE[0] * (1 - t * 0.25) + BLUE_DARK[0] * (t * 0.25))
            g = int(BLUE[1] * (1 - t * 0.25) + BLUE_DARK[1] * (t * 0.25))
            b = int(BLUE[2] * (1 - t * 0.25) + BLUE_DARK[2] * (t * 0.25))
            alpha = 255

            # Clock ring
            dist = math.hypot(px - c, py - c)
            if abs(dist - ring_r) <= ring_w:
                r, g, b = WHITE
            else:
                # Hands
                if (segment_distance(px, py, c, c, hx, hy) <= hand_w / 2 or
                        segment_distance(px, py, c, c, mx, my) <= hand_w / 2):
                    r, g, b = WHITE
                elif dist <= n * 0.045:
                    r, g, b = WHITE

            row += bytes((r, g, b, alpha))
        rows.append(row)

    # Box-downsample to the requested size.
    out = bytearray()
    for y in range(size):
        out.append(0)  # PNG filter: none
        for x in range(size):
            rs = gs = bs = as_ = 0
            for dy in range(ss):
                row = rows[y * ss + dy]
                for dx in range(ss):
                    i = (x * ss + dx) * 4
                    rs += row[i]; gs += row[i + 1]; bs += row[i + 2]; as_ += row[i + 3]
            count = ss * ss
            out += bytes((rs // count, gs // count, bs // count, as_ // count))
    return bytes(out)


def png(size):
    raw = render(size)
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) +
            chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def main():
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [(s, png(s)) for s in sizes]

    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    directory = b""
    for size, data in frames:
        directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(header + directory + b"".join(d for _, d in frames))
    print("wrote", os.path.abspath(OUT), os.path.getsize(OUT), "bytes")


if __name__ == "__main__":
    main()

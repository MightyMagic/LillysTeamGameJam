#!/usr/bin/env python3
"""Render original 16-bit-style stealth music, informed by the supplied MGS reference.

Only standard-library oscillators and seeded noise are used. No recording is
embedded. The slow drum backbeat sits under a faster, quiet repeating pulse.
"""
from array import array
from pathlib import Path
import math
import random
import struct
import wave

RATE = 22050
BPM = 110
BEAT = 60 / BPM
BARS = 32
COUNT = round(BARS * 4 * BEAT * RATE)
TAU = math.tau


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def render(path):
    left = array('f', [0.0]) * COUNT
    right = array('f', [0.0]) * COUNT
    rng = random.Random(1719110)

    def add(beat, seconds, voice, pan=0):
        first = round(beat * BEAT * RATE)
        gl, gr = math.sqrt((1-pan)/2), math.sqrt((1+pan)/2)
        for i in range(round(seconds * RATE)):
            sample = voice(i / RATE)
            at = (first+i) % COUNT
            left[at] += sample*gl
            right[at] += sample*gr

    def triangle(phase):
        # Band-limited triangle: rounded retro bass, without aliased edges.
        return (math.sin(phase)-math.sin(3*phase)/9+math.sin(5*phase)/25
                -math.sin(7*phase)/49)

    def pulse(phase, frequency, duty=.3):
        # Restrained pulse oscillator, with upper partials rolled off.
        return sum(math.sin(math.pi*h*duty)*math.cos(h*phase-math.pi*h*duty)
                   / (h*(1+(h*frequency/1900)**2)) for h in range(1, 8))

    def bass(beat, note, length, gain):
        f, duration = hz(note), length*BEAT
        def voice(t):
            env = min(1, t/.008)*min(1, max(0, duration-t)/.065)
            env *= .35+.65*math.exp(-t*4.5)
            phase = TAU*f*t
            return gain*env*(.85*triangle(phase)+.24*math.sin(phase*.5)
                             +.12*math.sin(phase*2)*math.exp(-t*8))
        add(beat, duration, voice)

    def pluck(beat, note, gain, pan, duration=.48):
        f = hz(note)
        def voice(t):
            env = min(1, t/.004)*math.exp(-t*10)
            phase = TAU*f*t+.2*math.sin(TAU*f*2*t)*math.exp(-t*16)
            return gain*env*(.6*pulse(phase, f)+.35*math.sin(phase))
        add(beat, duration, voice, pan)
        # Short, quiet opposite-channel echo, also wrapped around the loop.
        add(beat+.75, duration, lambda t: .19*voice(t), -pan)

    def kick(beat, gain):
        def voice(t):
            phase = TAU*(49*t+.75*(1-math.exp(-t*40)))
            return gain*min(1, t/.002)*math.exp(-t*19)*math.sin(phase)
        add(beat, .38, voice)

    def noise_hit(beat, gain, kind, pan=0):
        duration = .18 if kind == 'snare' else .075
        values = [rng.uniform(-1, 1) for _ in range(round(duration*RATE))]
        low, filtered = 0., []
        for value in values:
            low += .25*(value-low)
            filtered.append(low)
        def voice(t):
            i = min(len(values)-1, round(t*RATE))
            env = min(1, t/.0015)
            if kind == 'snare':
                snap = filtered[i]*math.exp(-t*28)
                rim = .36*math.sin(TAU*193*t)*math.exp(-t*50)
                return gain*env*(snap+rim)
            return gain*env*math.exp(-t*75)*(values[i]-filtered[i])
        add(beat, duration, voice, pan)

    def tom(beat, gain, pitch=153, pan=0):
        def voice(t):
            phase = TAU*(pitch*t+.35*(1-math.exp(-t*55)))
            return gain*min(1, t/.0015)*math.exp(-t*32)*(
                math.sin(phase)+.25*math.sin(phase*1.57))
        add(beat, .19, voice, pan)

    # The recording's strongest pitched centres are A, E, F and D. Keep a long
    # A pedal and the darker F/D movement, rather than a pop chord every bar.
    roots = ([45]*8+[41]*4+[38]*4)*2
    for bar, root in enumerate(roots):
        base, section = bar*4, bar % 16
        bass(base, root, 1.23, .32)
        bass(base+1.5, root, .34, .19)
        bass(base+2.25, root, .60, .25)
        bass(base+3.25, root+7 if bar % 4 == 3 else root, .37, .17)

        # Half-time drums: one main snare per bar (perceived backbeat 55 BPM).
        kick(base, .40)
        kick(base+1.75, .24)
        if bar % 2:
            kick(base+3.5, .20)
        noise_hit(base+2, .23, 'snare')
        for step in range(8):
            gain = [.047, .024, .037, .028, .053, .024, .037, .029][step]
            noise_hit(base+step*.5, gain*rng.uniform(.92, 1.05), 'hat',
                      -.25 if step % 2 else .25)
        # Small, dry electronic tom replies supply motion without a busy break.
        for off, pitch, gain, pan in [(1.25, 184, .065, -.22),
                                      (2.75, 147, .09, .22),
                                      (3.25, 184, .048, -.22)]:
            tom(base+off, gain, pitch, pan)
        if bar % 8 == 7:
            tom(base+3.75, .085, 123, .15)

        # A quiet repeating middle-register figure; not a foreground lead.
        # Distinct pulse timbre makes the arrangement read as 16-bit game music.
        if root == 45:
            notes = [57, 64, 57, 60, 64, 57, 59, 64]
        elif root == 41:
            notes = [53, 60, 57, 64, 60, 57, 64, 60]
        else:
            notes = [50, 57, 60, 64, 57, 60, 62, 57]
        for step, note in enumerate(notes):
            if section in (7, 15) and step >= 6:
                continue  # a breath before the next phrase
            gain = .063 if step % 2 == 0 else .038
            pluck(base+step*.5+.015, note, gain,
                  -.28 if step % 2 else .28)

        # Sparse sustained intervals, supporting tension without returning to
        # the earlier ambient-pad arrangement. Change the answer in pass two.
        if bar % 4 in (1, 3):
            upper = [76, 71] if root == 45 else ([76, 72] if root == 41 else [76, 74])
            if bar >= 16:
                upper.reverse()
            for off, note in zip((.75, 3), upper):
                f = hz(note)
                def reply(t, f=f):
                    env = min(1, t/.025)*math.exp(-t*4.8)
                    return .025*env*(math.sin(TAU*f*t)+.16*math.sin(TAU*f*3*t))
                add(base+off, 1.1, reply, -.4)
                add(base+off+.75, 1.1, lambda t, voice=reply: .25*voice(t), .4)

    # Keep loudness close to the previous asset so the existing 15% game mix
    # and alert ducking remain useful. Event tails wrap for gapless looping.
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right))
    rms = math.sqrt(sum(l*l+r*r for l, r in zip(left, right))/(2*COUNT))
    gain = min(.72/max(peak, .001), .12/max(rms, .001))
    seam = round(.002*RATE)
    for channel in (left, right):
        join = (channel[0]+channel[-1])*.5
        for i in range(seam):
            t = i/(seam-1)
            channel[i] = join*(1-t)+channel[i]*t
            channel[-1-i] = join*(1-t)+channel[-1-i]*t
    pcm = bytearray(COUNT*4)
    for i, (l, r) in enumerate(zip(left, right)):
        struct.pack_into('<hh', pcm, i*4, round(l*gain*32767), round(r*gain*32767))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm)
    print(f'{path}: {COUNT/RATE:.3f}s, {BPM} BPM (half-time drums), '
          f'stereo {RATE} Hz, peak={peak*gain:.3f}, RMS={rms*gain:.3f}')


if __name__ == '__main__':
    render(Path(__file__).resolve().parents[1] / 'Assets/Resources/Audio/Music/stealth_loop.wav')

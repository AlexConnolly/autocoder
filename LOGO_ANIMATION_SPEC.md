# Spec: Cooler Animated Logo

## Overview

Redesign the `LogoMark` SVG in the header to be visually richer and add a periodic "idle animation" that fires occasionally (every 20–45 seconds at random) rather than looping constantly. The animation reinforces the autocoder brand theme of automated processing without being distracting during normal work.

---

## Affected Files

| File | Change |
|---|---|
| `frontend/src/components/layout/Header.tsx` | Redesign `LogoMark` SVG; convert to stateful component with periodic animation trigger |
| `frontend/src/index.css` | Add `@keyframes` and scoped CSS rules for the animation sequence |

No new npm dependencies required. No changes to `tailwind.config.ts` or `vite.config.ts`.

---

## Current State

**File:** `frontend/src/components/layout/Header.tsx`, lines 25–34

`LogoMark` is a stateless functional component returning a 16×16 inline SVG of four rounded rectangles in a 2×2 grid. All four rects are filled `#6366f1` (indigo-500) at descending opacity: 1.0 / 0.6 / 0.6 / 0.3. There is no animation, no React state, and no props. The component is used once in the header brand section (line 41).

---

## Proposed Changes

### 1. Redesigned SVG ("Cooler")

Replace the four flat, flat-opacity rectangles with a design that has more visual depth and brand identity.

**Recommended design: gradient grid with code-bracket detail**

- Increase the `viewBox` from `0 0 16 16` to `0 0 20 20` and render at `18×18` px (up from 16×16) so the finer detail is legible
- Add a `<defs>` block containing a `<linearGradient id="logo-gradient">` running diagonally from `#818cf8` (indigo-400, top-left) to `#4338ca` (indigo-700, bottom-right). This gives the mark visual depth that the flat opacity approach lacks
- Apply the gradient as the `fill` on all four rects instead of the hardcoded `#6366f1` with opacity overrides
- Vary the visual weight between the four rects via `opacity` (top-left 1.0, top-right 0.75, bottom-left 0.75, bottom-right 0.5) so the existing diagonal-fade brand language is preserved but rendered with richer color
- Inside the top-left rect, add two short `<path>` strokes forming `</>` brackets: a `<` chevron and a `>` chevron separated by a `/` diagonal. These are 1px white strokes at 60% opacity, anchoring the "coder" identity directly in the mark. At 18px the brackets will be approximately 5×4 px and visible at 2× but gracefully illegible at 1× — purely decorative at small sizes
- Assign each rect a CSS `className` (`logo-b1` through `logo-b4`) to enable targeted animation

**Design reference (approximate SVG structure):**

```svg
<svg width="18" height="18" viewBox="0 0 20 20" fill="none" xmlns="...">
  <defs>
    <linearGradient id="logo-gradient" x1="0" y1="0" x2="20" y2="20" gradientUnits="userSpaceOnUse">
      <stop offset="0%"   stopColor="#818cf8" />
      <stop offset="100%" stopColor="#4338ca" />
    </linearGradient>
  </defs>
  <!-- top-left: full opacity, contains </>  -->
  <rect className="logo-b1" x="1" y="1" width="8" height="8" rx="2" fill="url(#logo-gradient)" />
  <!-- <> bracket paths inside top-left rect, white 60% -->
  <path d="M3.5 5 L2.5 5.8 L3.5 6.6"   stroke="white" strokeOpacity="0.6" strokeWidth="1" strokeLinecap="round" />
  <path d="M5.5 4.2 L6.5 5.5 L5.5 6.8" stroke="white" strokeOpacity="0.6" strokeWidth="1" strokeLinecap="round" />
  <path d="M4.8 4 L4.2 7"               stroke="white" strokeOpacity="0.6" strokeWidth="1" strokeLinecap="round" />
  <!-- top-right: 0.75 opacity -->
  <rect className="logo-b2" x="11" y="1" width="8" height="8" rx="2" fill="url(#logo-gradient)" opacity="0.75" />
  <!-- bottom-left: 0.75 opacity -->
  <rect className="logo-b3" x="1"  y="11" width="8" height="8" rx="2" fill="url(#logo-gradient)" opacity="0.75" />
  <!-- bottom-right: 0.5 opacity -->
  <rect className="logo-b4" x="11" y="11" width="8" height="8" rx="2" fill="url(#logo-gradient)" opacity="0.5" />
</svg>
```

*Exact path coordinates for the `</>` brackets should be tuned visually in the browser.*

---

### 2. Periodic Animation ("Animated Occasionally")

**Trigger mechanism:** React `useState` + `useEffect` in `LogoMark`. A random timer fires every 20–45 seconds (using `Math.random()` to pick within that range). When the timer fires, a CSS class `logo-animating` is added to the SVG root for 1,100 ms, then removed. The next timer is scheduled immediately after removal.

**Animation type: sequential block pulse**

Each rect "pulses" — briefly scales up 15% and increases brightness — in sequence: top-left → top-right → bottom-left → bottom-right, with a 150 ms stagger between each. The effect reads as the four blocks "thinking" or "processing," consistent with the tool's purpose as an AI coding assistant.

Total animation duration: 450 ms (last block delay) + 600 ms (block animation duration) = 1,050 ms. The `logo-animating` class is removed after 1,100 ms.

**CSS keyframe (added to `frontend/src/index.css`, inside `@layer utilities` or after it):**

```css
@media (prefers-reduced-motion: no-preference) {
  @keyframes logo-block-pulse {
    0%   { transform: scale(1);    filter: brightness(1); }
    35%  { transform: scale(1.18); filter: brightness(1.5); }
    65%  { transform: scale(1);    filter: brightness(1); }
    100% { transform: scale(1);    filter: brightness(1); }
  }

  .logo-animating .logo-b1 {
    animation: logo-block-pulse 0.6s ease-in-out 0ms   forwards;
    transform-origin: center;
  }
  .logo-animating .logo-b2 {
    animation: logo-block-pulse 0.6s ease-in-out 150ms forwards;
    transform-origin: center;
  }
  .logo-animating .logo-b3 {
    animation: logo-block-pulse 0.6s ease-in-out 300ms forwards;
    transform-origin: center;
  }
  .logo-animating .logo-b4 {
    animation: logo-block-pulse 0.6s ease-in-out 450ms forwards;
    transform-origin: center;
  }
}
```

*`transform-origin: center` is required on each rect rather than on the SVG root so each block scales from its own center.*

**React state logic in `LogoMark` (pseudocode):**

```tsx
function LogoMark() {
  const [animating, setAnimating] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    function scheduleNext() {
      const delay = 20_000 + Math.random() * 25_000; // 20–45 s
      timerRef.current = setTimeout(() => {
        setAnimating(true);
        timerRef.current = setTimeout(() => {
          setAnimating(false);
          scheduleNext();
        }, 1_100);
      }, delay);
    }
    scheduleNext();
    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current);
    };
  }, []);

  return (
    <svg
      width="18" height="18" viewBox="0 0 20 20"
      className={animating ? 'logo-animating' : undefined}
      ...
    >
      ...
    </svg>
  );
}
```

The `useRef` holds the current timer ID so the cleanup always cancels the most recent pending call on unmount.

---

## Implementation Steps (ordered)

1. **`frontend/src/index.css`** — Append the `@keyframes logo-block-pulse` definition and the four `.logo-animating .logo-bN` rules, all inside a `@media (prefers-reduced-motion: no-preference)` block. Place after the existing `@layer utilities` block to avoid ordering conflicts.

2. **`frontend/src/components/layout/Header.tsx`** — Update the React import at the top to include `useEffect`, `useRef`, `useState` (currently there is no explicit React import — the file uses JSX transform without an import, so add `import { useEffect, useRef, useState } from 'react';`).

3. **`Header.tsx`** — Rewrite the `LogoMark` function:
   - Add `useState<boolean>(false)` for `animating`
   - Add `useRef<ReturnType<typeof setTimeout> | null>(null)` for `timerRef`
   - Add the `useEffect` scheduling logic
   - Replace the SVG: new `viewBox`, `<defs>` with gradient, four rects with `className` attributes, `</>` bracket paths in top-left rect, conditional `logo-animating` class on `<svg>`

4. **Verify** in the browser (dev server at port 5173):
   - Logo renders correctly in light and dark themes
   - The `</>` bracket detail is visible at normal zoom
   - The animation fires after a 20–45 s wait and cycles smoothly
   - No animation plays when the OS has reduced motion enabled

---

## Risks and Edge Cases

| Risk | Details | Mitigation |
|---|---|---|
| `filter: brightness()` in SVG `<rect>` elements | SVG elements support CSS `filter` in modern browsers but behaviour in older Chromium/Safari can be inconsistent inside inline SVG. | Test in both Chrome and Safari. If brightness filter misbehaves, replace with `opacity` oscillation (0.5 → 1.0 → 0.5) as a fallback — slightly less punchy but universally supported. |
| Timer leak on fast navigation | If the user navigates away and back quickly, two timer chains could coexist. | The `useEffect` cleanup clears `timerRef.current` on unmount. The inner `setTimeout` for the 1,100 ms clear is also stored in `timerRef`, so a late unmount always cancels both. |
| SVG `<linearGradient id>` collision | If `LogoMark` were ever rendered more than once on the same page, two `<defs>` elements with `id="logo-gradient"` would exist in the DOM, and the second could shadow the first. | Currently `LogoMark` is rendered exactly once in `Header.tsx`. No additional guard is needed, but if it were ever reused, the gradient `id` would need to be unique per instance (e.g. via a stable `useId()` from React 18). |
| `transform-origin` inside SVG | CSS `transform-origin` on SVG elements uses the SVG coordinate system, not the element's local bounding box in some browsers. Setting `transform-box: fill-box` alongside `transform-origin: center` fixes this. | Add `transform-box: fill-box; transform-origin: center;` to each `.logo-bN` rule. |
| `</>` bracket paths at 1× density | At 96 DPI / 1× zoom the bracket paths at ~5px span may render as a blur. | The brackets are decorative; set `shape-rendering: crispEdges` on the bracket paths to hint the renderer, and verify at 1×. If they remain illegible, accept it — the mark reads as a structured grid at that size regardless. |
| Animation firing while user is typing | The animation is purely visual on a 16–18 px icon and does not affect layout, focus, or any interactive element. | No mitigation needed; low blast radius. |

---

## Out of Scope

- Adding Framer Motion, Lottie, or any animation library — pure CSS keyframes are sufficient and add no bundle weight
- Animating the logo on hover (can be added trivially later with a CSS `:hover` variant)
- Changing the "autocoder" wordmark (font, weight, size)
- Exporting the logo as a standalone SVG asset file — keeping it inline avoids HTTP requests and allows CSS class targeting
- Changing the logo on the SettingsPage (which uses a back-button instead of the logo mark)

---

## Summary

Two files change. `Header.tsx` gains a stateful `LogoMark` with a redesigned SVG (gradient fill, `</>` code-bracket inset, gradient rects with className handles) and a `useEffect`-driven random timer that adds/removes a `logo-animating` class every 20–45 seconds. `index.css` gains the matching `@keyframes logo-block-pulse` and four staggered selector rules, all scoped to `prefers-reduced-motion: no-preference`. No new packages. Estimated diff: 60–80 lines across two files.

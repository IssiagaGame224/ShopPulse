// Préférence système « réduire les animations » (WCAG) — lue côté navigateur,
// jamais au prerender. Module importé dynamiquement (pattern js/stripe-payment.js).
export function prefersReducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

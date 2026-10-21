// The things the view needs that CSS and Blazor cannot do on their own.
window.wrestleSim = window.wrestleSim || {};

// Keep the keyboard cursor visible. The sheet owns the keydown handler, so focus stays on
// the sheet rather than hopping row to row — which means nothing scrolls the row into view
// for us. Measured before this: three ArrowDowns moved the cursor to row 4 while the list's
// scrollTop stayed at 0 and the page *behind the modal* scrolled 531px.
window.wrestleSim.revealRow = function (sheet, index) {
    if (!sheet) return;
    const rows = sheet.querySelectorAll('.prow');
    const row = rows[index];
    if (row) row.scrollIntoView({ block: 'nearest' });
};

// Lock the page behind a sheet. Without it, a wheel over the strip of scrim above the sheet
// scrolled the page underneath — 0 to 522px at 390px wide.
window.wrestleSim.lockScroll = function (locked) {
    document.body.style.overflow = locked ? 'hidden' : '';
};

// Put a new view at the top. Blazor swaps the component tree without touching the scroll
// position, so a screen or a wizard step opens wherever the last one was left — see ViewScroll
// for what that cost on the match result.
window.wrestleSim.toTop = function () {
    window.scrollTo(0, 0);
};

// Whether the viewer has asked for less motion. Read once per match rather than wired to a
// change event: a preference that flips mid-reveal is not worth the subscription, and the
// next match reads it again.
window.wrestleSim.reducedMotion = function () {
    return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
};

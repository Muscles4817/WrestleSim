// Two things a modal sheet needs that CSS and Blazor cannot do on their own.
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

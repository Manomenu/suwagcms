export const HEADER_HEIGHT = 60;
export const PANEL_WIDTH = 220;
export const CONTENT_MAX_WIDTH = 1400;
export const LOGO_SIZE = 28;

// Why: Suwageval's panel is on the left and this one line flips it to the right, border and all.
export const PANEL_SIDE: "left" | "right" = "left";

// Why: one pure function owns everything that depends on the side, so flipping it needs no other edit.
export function panelSideClasses(side: "left" | "right"): { row: string; border: string; overlay: string } {
    return side === "left"
        ? { row: "flex-row", border: "border-r", overlay: "max-md:left-0" }
        : { row: "flex-row-reverse", border: "border-l", overlay: "max-md:right-0" };
}

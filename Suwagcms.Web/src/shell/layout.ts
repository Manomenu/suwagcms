export const HEADER_HEIGHT = 60;
export const PANEL_WIDTH = 220;
export const CONTENT_MAX_WIDTH = 1400;
export const LOGO_SIZE = 28;

export const PANEL_SIDE: "left" | "right" = "left";

export function panelSideClasses(side: "left" | "right"): { row: string; border: string; overlay: string } {
    return side === "left"
        ? { row: "flex-row", border: "border-r", overlay: "max-md:left-0" }
        : { row: "flex-row-reverse", border: "border-l", overlay: "max-md:right-0" };
}

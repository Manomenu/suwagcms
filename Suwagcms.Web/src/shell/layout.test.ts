import { describe, expect, it } from "vitest";

import { panelSideClasses } from "./layout";

describe("panelSideClasses", () => {
    it("AC-web-shell-5: puts the panel and its border on the left", () => {
        expect(panelSideClasses("left")).toMatchObject({ row: "flex-row", border: "border-r" });
    });

    it("AC-web-shell-5: puts the panel and its border on the right", () => {
        expect(panelSideClasses("right")).toMatchObject({ row: "flex-row-reverse", border: "border-l" });
    });
});

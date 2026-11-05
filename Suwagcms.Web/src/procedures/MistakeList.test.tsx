import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { MistakeList } from "./MistakeList";

describe("MistakeList", () => {
    it("AC-procedure-creator-16: when the live check cannot reach the server, the list says so instead of looking empty", () => {
        const html = renderToStaticMarkup(
            <MistakeList check={{ state: "unreachable", message: "The server could not be reached." }} onPick={() => undefined} />,
        );

        expect(html).toContain("Could not check: The server could not be reached.");
        expect(html).not.toContain("No mistakes");
    });
});

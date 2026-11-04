import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { App } from "./App";

describe("App", () => {
    it("shows the name of the product as the page heading", () => {
        expect(renderToStaticMarkup(<App />)).toBe("<h1>Suwagcms</h1>");
    });
});

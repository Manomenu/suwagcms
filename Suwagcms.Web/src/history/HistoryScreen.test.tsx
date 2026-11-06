import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";

import { HistoryScreen } from "./HistoryScreen";

describe("HistoryScreen", () => {
    it("AC-change-history-20: a bot's History page has its title and no heading repeating it", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/sklep/history">
                <PageSizeContext value={{ size: 20, failure: null }}>
                    <HistoryScreen bot="sklep" />
                </PageSizeContext>
            </Router>,
        );

        expect(html.match(/<h\d[^>]*>[^<]*<\/h\d>/g)).toEqual([expect.stringContaining("History of sklep")]);
    });
});

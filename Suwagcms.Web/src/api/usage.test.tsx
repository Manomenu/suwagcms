import { UsagePanel } from "@maniumek/suwfrontkit";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { usageLinkOf, type UsageLink } from "./usage";

const usedBy: UsageLink[] = [
    { kind: "article", id: "wycena", field: "content" },
    { kind: "procedure", id: "rabaty", field: "steps.2.inform" },
];
const uses: UsageLink[] = [{ kind: "fact", id: "customer.city", field: "content" }];

describe("usageLinkOf", () => {
    it("AC-delete-guard-7: every related resource is listed with its field as a link to its edit screen", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/rzeczoznawca/articles">
                <UsagePanel usage={{ usedBy, uses }} failure={null} linkOf={usageLinkOf("rzeczoznawca")} />
            </Router>,
        );

        expect(html).toContain('href="/bots/rzeczoznawca/articles/wycena/edit"');
        expect(html).toContain('href="/bots/rzeczoznawca/procedures/rabaty/edit"');
        expect(html).toContain('href="/bots/rzeczoznawca/facts/customer.city/edit"');
        expect(html).toContain("article · content");
        expect(html).toContain("procedure · steps.2.inform");
        expect(html).toContain("fact · content");
    });
});

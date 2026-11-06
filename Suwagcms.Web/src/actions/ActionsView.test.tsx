import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";

import type { ActionView } from "./api";
import { ActionsView } from "./ActionsView";

const action = (id: string): ActionView =>
    ({
        id,
        description: `What ${id} does`,
        implementation: { http: { method: "GET", url: "https://crm.internal/x" } },
        usedBy: [],
    }) as unknown as ActionView;

describe("ActionsView", () => {
    it("AC-availability-26: the list says which action is off", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/rzeczoznawca/actions">
                <PageSizeContext value={{ size: 20, failure: null }}>
                    <ActionsView
                        bot="rzeczoznawca"
                        actions={[action("check"), action("send")]}

                        isOff={(id) => id === "send"}
                        onAdd={() => undefined}
                        onOpen={() => undefined}
                    />
                </PageSizeContext>
            </Router>,
        );

        const rows = html.split("<tr>");
        expect(rows.find((row) => row.includes("What send does"))).toContain(">off<");
        expect(rows.find((row) => row.includes("What check does"))).toContain(">on<");
    });
});

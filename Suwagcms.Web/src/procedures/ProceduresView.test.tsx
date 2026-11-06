import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import { PageSizeContext } from "@/api/pagination";

import type { ProcedureList } from "./api";
import { ProceduresView } from "./ProceduresView";

const list: ProcedureList = {
    procedures: [
        { id: "conversation", description: "The base of every conversation", revision: 1, notReady: 0 },
        { id: "orderValuation", description: "The customer orders a valuation", revision: 2, notReady: 2 },
        { id: "goodbye", description: "Say goodbye", revision: 1, notReady: 1 },
    ],
    botNotReady: [],
};

function view(shown: ProcedureList): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots/rzeczoznawca/procedures">
            <PageSizeContext value={{ size: 20, failure: null }}>
                <ProceduresView onAdd={() => undefined} onOpen={() => undefined} bot="rzeczoznawca" list={shown} />
            </PageSizeContext>
        </Router>,
    );
}

describe("ProceduresView", () => {
    it("AC-availability-22: the list says which procedure is off", () => {
        const html = renderToStaticMarkup(
            <Router ssrPath="/bots/rzeczoznawca/procedures">
                <PageSizeContext value={{ size: 20, failure: null }}>
                    <ProceduresView
                        onAdd={() => undefined}
                        onOpen={() => undefined}
                        bot="rzeczoznawca"
                        list={list}
                        isOff={(id) => id === "goodbye"}
                    />
                </PageSizeContext>
            </Router>,
        );

        const rows = html.split("<tr>");
        expect(rows.filter((row) => row.includes("goodbye")).at(-1)).toContain(">off<");
        expect(rows.filter((row) => row.includes("orderValuation")).at(-1)).toContain(">on<");
    });

    it("AC-procedure-model-14: lists each procedure with its description and how many mistakes stop a version", () => {
        const html = view(list);

        expect(html).toContain("Procedures of rzeczoznawca");
        expect(html).toContain("The customer orders a valuation");
        expect(html).toContain("2 mistakes before a version");
        expect(html).toContain("1 mistake before a version");
        expect(html).toContain("ready");
    });

    it("AC-procedure-model-14: says what stops the whole bot, and what to do when there are no procedures", () => {
        const html = view({
            procedures: [],
            botNotReady: [
                {
                    code: "bot.conversation.missing",
                    stage: "Version",
                    resource: { kind: "Definition", id: "" },
                    path: "procedures",
                    message: "This bot has no 'conversation' procedure.",
                    fix: "Add one.",
                    related: [],
                },
            ],
        });

        expect(html).toContain("This bot has no &#x27;conversation&#x27; procedure.");
        expect(html).toContain("No procedures yet");
    });
});

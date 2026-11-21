// @vitest-environment jsdom
import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ResourceEdit } from "./resourceEdit";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const noAnswer = () => Promise.resolve({ lines: [], mistakes: [] });

function render(usedBy: number, onDelete: () => Promise<void>, deleteFailure: string | null = null) {
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);
    act(() => {
        root.render(
            <ResourceEdit
                noun="fact"
                plural="facts"
                addTitle="Add a fact"
                backHref="/list"
                ready
                missingId={null}
                editedId="customer.city"
                deleteFailure={deleteFailure}
                dirty={false}
                changingId={false}
                onChangingId={() => undefined}
                preview={noAnswer}
                apply={noAnswer}
                onChanged={() => undefined}
                usage={{
                    usage: {
                        usedBy: Array.from({ length: usedBy }, (_, index) => ({ kind: "procedure", id: `p${String(index)}`, field: "f" })),
                        uses: [],
                    },
                    failure: null,
                    hrefOf: () => "/",
                }}
                onDelete={onDelete}
                history={{
                    entries: [],
                    since: null,
                    failure: null,
                    paging: { page: 1, pages: 1, asking: false, failure: null, onMove: () => undefined },
                }}
            >
                <p>form</p>
            </ResourceEdit>,
        );
    });
    return root;
}

const button = (text: string) => [...document.body.querySelectorAll("button")].find((one) => one.textContent === text);

afterEach(() => {
    document.body.replaceChildren();
});

describe("ResourceEdit", () => {
    it("AC-delete-guard-9: Delete is disabled with the reason beside it while something uses the resource", () => {
        render(2, () => Promise.resolve());

        expect(button("Delete")?.disabled).toBe(true);
        expect(document.body.textContent).toContain("Used in 2 places. Remove those references first.");
    });

    it("AC-delete-guard-9: Delete works, with no reason shown, when nothing uses the resource", () => {
        render(0, () => Promise.resolve());

        expect(button("Delete")?.disabled).toBe(false);
        expect(document.body.textContent).not.toContain("Remove those references first");
    });

    it("AC-delete-guard-20: pressing Delete deletes nothing yet: the question comes first, and a refusal stays on screen", () => {
        const onDelete = vi.fn(() => Promise.resolve());
        render(0, onDelete, "'priceList' is in the knowledge base 'oferta'.");

        expect(document.body.querySelector('[role="alertdialog"]')).toBeNull();
        expect(document.body.textContent).toContain("'priceList' is in the knowledge base");
        act(() => {
            button("Delete")?.click();
        });

        expect(document.body.querySelector('[role="alertdialog"]')?.textContent).toContain("Delete fact customer.city?");
        expect(onDelete).not.toHaveBeenCalled();
    });
});

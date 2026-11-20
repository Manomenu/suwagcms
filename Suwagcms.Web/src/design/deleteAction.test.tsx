// @vitest-environment jsdom
import { act } from "react";
import { createRoot } from "react-dom/client";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

import { DeleteAction } from "./deleteAction";

function action(usedBy: number, failure: string | null = null): string {
    return renderToStaticMarkup(
        <DeleteAction noun="fact" id="customer.city" usedBy={usedBy} failure={failure} onDelete={() => Promise.resolve()} />,
    );
}

describe("DeleteAction", () => {
    it("AC-delete-guard-9: Delete is disabled with the reason beside it while something uses the resource", () => {
        const html = action(2);

        expect(html).toMatch(/<button[^>]*disabled=""[^>]*>Delete<\/button>/);
        expect(html).toContain("Used in 2 places. Remove those references first.");
    });

    it("AC-delete-guard-9: Delete works, with no reason shown, when nothing uses the resource", () => {
        const html = action(0);

        expect(html).toContain(">Delete</button>");
        expect(html).not.toContain('disabled=""');
        expect(html).not.toContain("Remove those references first");
    });

    it("AC-delete-guard-20: pressing Delete deletes nothing yet: the question comes first, and a refusal stays on screen", () => {
        const html = action(0, "'priceList' is in the knowledge base 'oferta'.");

        expect(html).not.toContain("alertdialog");
        expect(html).toContain("&#x27;priceList&#x27; is in the knowledge base");
    });

    it("AC-web-shell-18: while a deletion is on its way, Delete is disabled, so it cannot be asked for twice", () => {
        const onDelete = vi.fn(() => new Promise<void>(() => undefined));
        const container = document.createElement("div");
        document.body.append(container);
        const root = createRoot(container);
        act(() => {
            root.render(<DeleteAction noun="fact" id="customer.city" usedBy={0} failure={null} onDelete={onDelete} />);
        });
        const button = (text: string) => [...container.querySelectorAll("button")].find((one) => one.textContent === text);

        act(() => {
            button("Delete")?.click();
        });
        act(() => {
            button("Delete fact")?.click();
        });

        expect(onDelete).toHaveBeenCalledTimes(1);
        expect(button("Delete")?.disabled).toBe(true);
        act(() => {
            root.unmount();
        });
        container.remove();
    });
});

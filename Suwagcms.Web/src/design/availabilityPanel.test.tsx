import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { AvailabilityBadge, AvailabilityNotice, AvailabilityPanel, TurnOffDialog } from "./availabilityPanel";

const nothing = () => undefined;

describe("availability panel", () => {
    it("AC-availability-1: says on or off, and offers the opposite", () => {
        const on = renderToStaticMarkup(<AvailabilityPanel id="cennik" off={false} delivery={null} usedBy={[]} onTurn={nothing} />);
        const off = renderToStaticMarkup(<AvailabilityPanel id="cennik" off delivery={null} usedBy={[]} onTurn={nothing} />);

        expect(on).toContain(">on<");
        expect(on).toContain("Turn off");
        expect(off).toContain(">off<");
        expect(off).toContain("Turn on");
        expect(renderToStaticMarkup(<AvailabilityBadge off />)).toContain(">off<");
    });

    it("AC-availability-2: the question lists each thing that uses the resource with its field, and says when nothing does", () => {
        const used = renderToStaticMarkup(
            <TurnOffDialog
                id="cennik"
                usedBy={[{ kind: "article", id: "rabaty", field: "content" }]}
                onConfirm={nothing}
                onCancel={nothing}
            />,
        );
        const unused = renderToStaticMarkup(<TurnOffDialog id="cennik" usedBy={[]} onConfirm={nothing} onCancel={nothing} />);

        expect(used).toContain("Turn off cennik?");
        expect(used).toContain("rabaty");
        expect(used).toContain("article · content");
        expect(unused).toContain("Nothing uses it.");
    });

    it("AC-availability-2: turning off cannot be pressed before the list is loaded, and turning on can", () => {
        const loading = renderToStaticMarkup(<AvailabilityPanel id="cennik" off={false} delivery={null} usedBy={null} onTurn={nothing} />);
        const turningOn = renderToStaticMarkup(<AvailabilityPanel id="cennik" off delivery={null} usedBy={null} onTurn={nothing} />);

        expect(loading).toContain(`disabled=""`);
        expect(turningOn).not.toContain(`disabled=""`);
    });

    it("AC-availability-9: a confirmed setting says so, from the bot's answer", () => {
        const confirmed = renderToStaticMarkup(
            <AvailabilityPanel id="cennik" off delivery={{ state: "Confirmed" }} usedBy={[]} onTurn={nothing} />,
        );
        const notAsked = renderToStaticMarkup(
            <AvailabilityPanel id="cennik" off delivery={{ state: "NotAsked" }} usedBy={[]} onTurn={nothing} />,
        );

        expect(confirmed).toContain("Bot confirmed");
        expect(notAsked).not.toContain("Bot confirmed");
    });

    it("AC-availability-16: a setting not delivered says so with the reason and a way to send again", () => {
        const html = renderToStaticMarkup(
            <AvailabilityNotice delivery={{ state: "NotDelivered", reason: "cannot reach http://bot" }} failure={null} onSend={nothing} />,
        );
        const fine = renderToStaticMarkup(<AvailabilityNotice delivery={{ state: "Confirmed" }} failure={null} onSend={nothing} />);

        expect(html).toContain("Not delivered.");
        expect(html).toContain("cannot reach http://bot");
        expect(html).toContain("Send again");
        expect(fine).not.toContain("Not delivered.");
    });
});

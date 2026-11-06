// A person edits a procedure in the browser: the list of steps, the forms, the mistakes beside their place, and the save.
import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { createServer } from "node:http";
import type { AddressInfo } from "node:net";
import { testBots } from "../../e2e/bots";

const { freshId, newBot: newEmptyBot } = testBots("pc");

async function newBot(request: APIRequestContext): Promise<string> {
    const id = await newEmptyBot(request);
    for (const [name, data] of [
        ["customer.city", { type: "text", description: "The city", maxLength: 80 }],
        ["order.kind", { type: "enum", description: "What is valued", values: ["house", "flat"], writableBy: "correct" }],
        ["system.flag", { type: "text", description: "Set by a procedure", maxLength: 5, writableBy: "assign" }],
    ] as const) {
        expect((await request.put(`/api/bots/${id}/facts/${name}?ifRevision=0`, { data })).status()).toBe(200);
    }
    // Why: a bot with no `conversation` always has that one mistake, which would hide whether the page is ready.
    await agentSaves(request, id, "conversation", CONVERSATION, 0);
    return id;
}

const CONVERSATION = {
    description: "The base of every conversation",
    firstStep: "wait",
    steps: { wait: { listen: {}, next: "answer" }, answer: { say: "Hello", next: "wait" } },
    whenStalled: { instruction: "Say you could not help." },
};

const AGENT_WROTE = {
    description: "The customer orders a valuation of a property.",
    firstStep: "ask",
    steps: {
        ask: {
            collect: ["customer.city", "order.kind"],
            next: [{ when: { "order.kind": { oneOf: ["house"] } }, to: "mark" }, { to: "goodbye" }],
        },
        mark: { assign: { "system.flag": "big" }, next: "confirm" },
        confirm: { say: "Thank you, we will call you.", next: "ask" },
        goodbye: { say: "Sorry, we only value houses." },
    },
    whenStalled: { instruction: "Tell the customer you could not finish." },
};

async function agentSaves(request: APIRequestContext, bot: string, id: string, body: object, ifRevision: number): Promise<void> {
    expect((await request.put(`/api/bots/${bot}/procedures/${id}?ifRevision=${ifRevision}`, { data: body })).status()).toBe(200);
}

function row(page: Page, name: string) {
    return page.locator(`[data-step="${name}"]`);
}

async function open(page: Page, name: string): Promise<void> {
    await page.getByRole("button", { name: `Open step ${name}`, exact: true }).click();
}

async function addStep(page: Page, block: string, list = "Add step"): Promise<void> {
    await page.getByLabel(list, { exact: true }).selectOption(block);
}

async function stored<T>(request: APIRequestContext, bot: string, id: string): Promise<T> {
    return (await (await request.get(`/api/bots/${bot}/procedures/${id}`)).json()) as T;
}

test("AC-procedure-creator-1: an author builds a procedure with the forms, with an If, an Else if and an Else, and saves it", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/procedures`);
    await page.getByRole("button", { name: "Add a procedure" }).click();
    await page.getByLabel("Id").fill("orderValuation");
    await page.getByLabel("Description").fill("The customer orders a valuation of a property.");
    await page.getByRole("button", { name: "Create procedure" }).click();
    await expect(page.getByRole("heading", { name: "Procedure orderValuation" })).toBeVisible();
    await expect(page.getByText("'orderValuation' has no firstStep.")).toBeVisible();

    await addStep(page, "listen");
    await addStep(page, "collect");
    await page.getByLabel("customer.city").check();
    await page.getByLabel("order.kind").check();
    await expect(page.getByLabel("system.flag")).toHaveCount(0);

    await addStep(page, "if");
    await page.locator('[data-branch="If"]').getByRole("button", { name: "+ Choose a fact" }).click();
    await page.locator('[data-branch="If"]').getByLabel("Fact", { exact: true }).selectOption("order.kind");
    await page.locator('[data-branch="If"]').getByRole("checkbox", { name: "house" }).check();
    await page.getByRole("button", { name: "+ Else if" }).click();
    await page.locator('[data-branch="Else if"]').getByRole("button", { name: "+ Choose a fact" }).click();
    await page.locator('[data-branch="Else if"]').getByLabel("Condition", { exact: true }).selectOption("known");

    await addStep(page, "assign", "Add step to If of collect");
    await page.getByLabel("Assign another fact").selectOption("system.flag");
    await page.getByLabel("Value of system.flag").fill("big");
    await addStep(page, "say", "Add step to If of collect");
    await page.getByLabel("Say", { exact: true }).fill("Thank you, we will call you.");
    await addStep(page, "next", "Add step to If of collect");
    await page.locator('[data-row="next"]').getByLabel("Next step").selectOption("listen");
    await addStep(page, "next", "Add step to Else if of collect");
    await page.locator('[data-row="next"]').nth(1).getByLabel("Next step").selectOption("say");
    await addStep(page, "say", "Add step to Else of collect");
    await page.getByLabel("Say", { exact: true }).fill("Sorry, we only value houses.");
    await expect(row(page, "say2")).toBeVisible();

    await page.getByLabel("When stalled").fill("Tell the customer you could not finish.");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();

    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByText("Unsaved changes")).toHaveCount(0);
    const saved = await stored<{
        firstStep: string;
        revision: number;
        steps: Record<string, Record<string, unknown>>;
        notReady: unknown[];
    }>(request, bot, "orderValuation");
    expect(saved).toMatchObject({ firstStep: "listen", revision: 2, notReady: [] });
    expect(saved.steps["listen"]).toMatchObject({ next: "collect" });
    expect(saved.steps["collect"]).toMatchObject({
        collect: ["customer.city", "order.kind"],
        next: [
            { when: { "order.kind": { oneOf: ["house"] } }, to: "assign" },
            { when: { "customer.city": { known: true } }, to: "say" },
            { to: "say2" },
        ],
    });
    expect(saved.steps["assign"]).toMatchObject({ assign: { "system.flag": "big" }, next: "say" });
    expect(saved.steps["say"]).toMatchObject({ say: "Thank you, we will call you.", next: "listen" });
    expect(saved.steps["say2"]).toMatchObject({ say: "Sorry, we only value houses." });
});

test("AC-procedure-creator-2: a procedure an agent wrote opens with every step in its list, and AC-procedure-creator-29: saving is not offered until something changes", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await expect(page.locator("[data-step]")).toHaveCount(4);
    await expect(page.locator('[data-row="next"]').getByLabel("Next step")).toHaveValue("ask");
    await expect(page.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
    expect((await stored<{ revision: number }>(request, bot, "orderValuation")).revision).toBe(1);
});

test("AC-procedure-creator-23: the page has the shared edit screen's parts in their order, with Delete and History after the form", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await expect(page.getByRole("button", { name: "Back to procedures" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Procedure orderValuation" })).toBeVisible();
    // Why: the order on the page is the order in the document, since Save stays in view wherever the page has scrolled to.
    const parts = [
        page.getByRole("button", { name: "Change id" }),
        page.getByRole("region", { name: "Uses" }),
        page.getByLabel("Description"),
        page.getByRole("region", { name: "Steps" }),
        page.getByRole("button", { name: "Save", exact: true }),
        page.getByRole("button", { name: "Delete", exact: true }),
        page.getByRole("heading", { name: "History" }),
    ];
    for (const [index, part] of parts.slice(0, -1).entries()) {
        const next = await parts[index + 1]?.elementHandle();
        const follows = await part.evaluate(
            (one, other) => (one.compareDocumentPosition(other as Node) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0,
            next,
        );
        expect(follows, `part ${index} comes before part ${index + 1}`).toBe(true);
    }
});

test("AC-pagination-6: going back from a procedure's page returns to the list's page, without the step that was open", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    for (let index = 0; index < 25; index++) {
        await agentSaves(request, bot, `extra${index}`, { description: "D", firstStep: null, steps: {} }, 0);
    }
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation/edit?page=2&step=ask`);
    await expect(page.locator('section[aria-label="Step ask"]')).toBeVisible();

    await open(page, "ask");
    await expect(page).toHaveURL(/\?page=2$/);
    await page.getByRole("button", { name: "Back to procedures" }).click();

    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures\\?page=2$`));
});

test("AC-procedure-creator-45: on a long procedure Save stays in view and works while the page scrolls, and at the end covers neither the last row nor Delete", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const names = Array.from({ length: 40 }, (_, index) => `step${index}`);
    const steps = Object.fromEntries(
        names.map((name, index) => [name, { say: `Text ${index}`, ...(names[index + 1] === undefined ? {} : { next: names[index + 1] }) }]),
    );
    await agentSaves(request, bot, "longOne", { description: "D", firstStep: "step0", steps }, 0);
    await page.setViewportSize({ width: 1280, height: 700 });
    await page.goto(`/bots/${bot}/procedures/longOne/edit`);
    await page.getByLabel("Description").fill("Changed");

    const save = page.getByRole("button", { name: "Save", exact: true });
    await page.evaluate(() => {
        window.scrollTo(0, 900);
    });
    await expect(save).toBeInViewport({ ratio: 1 });
    await expect(row(page, "step0")).not.toBeInViewport();
    await save.click({ timeout: 2000, trial: true });

    await page.evaluate(() => {
        window.scrollTo(0, document.documentElement.scrollHeight);
    });
    const remove = page.getByRole("button", { name: "Delete", exact: true });
    await expect(remove).toBeVisible();
    const bottomOf = async (locator: ReturnType<Page["locator"]>) => {
        const box = await locator.boundingBox();
        if (box === null) throw new Error("not on screen");
        return { top: box.y, bottom: box.y + box.height };
    };
    const bar = await bottomOf(save);
    expect((await bottomOf(page.getByLabel("Add step", { exact: true }))).bottom).toBeLessThanOrEqual(bar.top);
    expect(bar.bottom).toBeLessThanOrEqual((await bottomOf(remove)).top);

    await page.evaluate(() => {
        window.scrollTo(0, 900);
    });
    await save.click();
    await expect(page.getByText("Unsaved changes")).toHaveCount(0);
    expect((await stored<{ description: string }>(request, bot, "longOne")).description).toBe("Changed");
});

test("AC-procedure-creator-45: a long action's Save stays in view and works while the page scrolls, as on a procedure", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const parameters = Object.fromEntries(
        Array.from({ length: 30 }, (_, index) => [`p${index}`, { type: "text", maxLength: 100, description: "Where" }]),
    );
    expect(
        (
            await request.put(`/api/bots/${bot}/actions/longOne?ifRevision=0`, {
                data: {
                    description: "Checks",
                    parameters,
                    results: null,
                    implementation: { http: { method: "POST", url: "https://x.internal", headers: null, timeoutSeconds: null } },
                },
            })
        ).status(),
    ).toBe(200);
    await page.setViewportSize({ width: 1280, height: 700 });
    await page.goto(`/bots/${bot}/actions/longOne/edit`);
    const description = page.getByLabel("Description", { exact: true }).first();
    await description.fill("Changed");

    const save = page.getByRole("button", { name: "Save", exact: true });
    await page.evaluate(() => {
        window.scrollTo(0, 900);
    });
    await expect(description).not.toBeInViewport();
    await expect(save).toBeInViewport({ ratio: 1 });
    await save.click();

    await expect
        .poll(async () => ((await (await request.get(`/api/bots/${bot}/actions/longOne`)).json()) as { description: string }).description)
        .toBe("Changed");
});

test("AC-procedure-creator-44: a branch with no step before End reads the validator's own fix beside it, and it goes when a row follows the If", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(
        request,
        bot,
        "orderValuation",
        {
            description: "D",
            firstStep: "ask",
            steps: {
                ask: { say: "A", next: [{ when: { "order.kind": { oneOf: ["house"] } }, to: "yes" }, { to: "no" }] },
                yes: { say: "Y" },
                no: { say: "N" },
            },
        },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    const otherwise = page.locator('[data-branch="Else"]');

    await page.getByRole("button", { name: "Delete step no" }).click();
    const note = otherwise.locator('[data-mistake="step.missing"]');
    await expect(note).toContainText("Add a step to this branch, or a Next: row.");
    await expect(note).toContainText("Refuses a save");

    await addStep(page, "say");
    await expect(note).toHaveCount(0);

    await addStep(page, "end", "Add step to Else of ask");
    await expect(otherwise.locator('[data-row="end"]')).toBeVisible();
    await expect(note).toContainText("Add a step to this branch, or a Next: row.");
});

test("AC-procedure-creator-8: a fresh If takes its condition on its row, with no step open, from a global or a local fact, and saves; Remove this If asks first and takes the whole If", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(
        request,
        bot,
        "orderValuation",
        {
            description: "D",
            firstStep: "ask",
            steps: { ask: { say: "A" } },
            localFacts: { visitDate: { type: "text", description: "When", maxLength: 20 } },
        },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await addStep(page, "if");
    const first = page.locator('[data-branch="If"]');
    await first.getByRole("button", { name: "+ Choose a fact" }).click();
    await first.getByLabel("Fact", { exact: true }).selectOption("order.kind");
    await first.getByRole("checkbox", { name: "house" }).check();
    await page.getByRole("button", { name: "+ Else if" }).click();
    const second = page.locator('[data-branch="Else if"]');
    await second.getByRole("button", { name: "+ Choose a fact" }).click();
    await second.getByLabel("Fact", { exact: true }).selectOption("visitDate");
    await second.getByLabel("Condition", { exact: true }).selectOption("known");
    for (const branch of ["If", "Else if", "Else"]) {
        await addStep(page, "say", `Add step to ${branch} of ask`);
        await page.getByLabel("Say", { exact: true }).fill(`In ${branch}`);
    }
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect
        .poll(async () => {
            const saved = await stored<{ steps: { ask: { next?: { when?: unknown }[] } } }>(request, bot, "orderValuation");
            return saved.steps.ask.next?.map((rule) => rule.when ?? null);
        })
        .toMatchObject([{ "order.kind": { oneOf: ["house"] } }, { visitDate: { known: true } }, null]);

    await expect(page.getByRole("button", { name: "Remove this If", exact: true })).toHaveCount(1);
    await page.getByRole("button", { name: "Remove this If", exact: true }).click();
    await page.getByRole("button", { name: "Keep", exact: true }).click();
    await expect(page.locator("[data-branch]")).toHaveCount(3);
    await page.getByRole("button", { name: "Remove this If", exact: true }).click();
    await page.getByRole("button", { name: "Remove them" }).click();
    await expect(page.locator("[data-branch]")).toHaveCount(0);
});

test("AC-procedure-creator-8: + and adds a second condition to a branch, and the save stores both", async ({ page, request }) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    const branch = page.locator('[data-branch="If"]');
    await branch.getByRole("button", { name: "+ and" }).click();
    await branch.getByLabel("Condition", { exact: true }).nth(1).selectOption("known");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect
        .poll(async () => {
            const saved = await stored<{ steps: { ask: { next: { when?: unknown }[] } } }>(request, bot, "orderValuation");
            return saved.steps.ask.next[0]?.when;
        })
        .toMatchObject({ "order.kind": { oneOf: ["house"] }, "customer.city": { known: true } });
});

test("AC-procedure-creator-30: a stored step no path reaches shows under Not connected with its mistake, and moving it gives it the next of its place; with no firstStep every step is there", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(
        request,
        bot,
        "orderValuation",
        { description: "D", firstStep: "a", steps: { a: { say: "A" }, lost: { say: "Lost" } } },
        0,
    );
    await agentSaves(request, bot, "noStart", { description: "D", firstStep: null, steps: { a: { say: "A" }, b: { say: "B" } } }, 0);

    await page.goto(`/bots/${bot}/procedures/noStart`);
    const section = page.getByRole("region", { name: "Not connected" });
    await expect(section.locator('[data-row="not-connected"]')).toHaveCount(2);

    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(section.locator('[data-row="not-connected"]')).toHaveCount(1);
    await expect(section).toContainText("Lost");
    await expect(section.locator('[data-mistake="step.unreachable"]')).toBeVisible();
    await expect(page.getByRole("button", { name: "Save", exact: true })).toBeDisabled();

    await section.getByRole("button", { name: "Move into the list" }).click();
    await expect(section).toHaveCount(0);
    await expect(row(page, "lost")).toBeVisible();
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect
        .poll(async () => (await stored<{ steps: unknown }>(request, bot, "orderValuation")).steps)
        .toMatchObject({ a: { say: "A", next: "lost" }, lost: { say: "Lost" } });
});

test("AC-procedure-creator-4: a mistake beside its step says what it stops and its fix, the count goes to it, and AC-procedure-creator-3: it goes when undone by hand", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();

    await open(page, "goodbye");
    await page.getByLabel("Say", { exact: true }).fill("");

    const note = page.locator('[data-mistake="step.block.empty"]');
    await expect(note).toContainText("Stops a version");
    await expect(note).toContainText("say");
    await open(page, "goodbye");
    await page.getByRole("region", { name: "Mistakes" }).getByRole("button").first().click();
    await expect(page).toHaveURL(/[?&]step=goodbye/);
    await expect(row(page, "goodbye")).toBeInViewport();

    await page.getByLabel("Say", { exact: true }).fill("Sorry, we only value houses.");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();
    expect((await stored<{ revision: number }>(request, bot, "orderValuation")).revision).toBe(1);
});

test("AC-procedure-creator-3: a mistake with a field goes beside it without saving, with its fix, and nothing was saved", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await page.getByLabel("When stalled").fill("");

    await expect(page.locator('[data-place="top"], [data-mistake="procedure.collect.noWhenStalled"]').first()).toContainText("whenStalled");
    await page.getByLabel("When stalled").fill("Tell the customer you could not finish.");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();
    expect((await stored<{ revision: number }>(request, bot, "orderValuation")).revision).toBe(1);
});

test("AC-procedure-creator-10: deleting a step a Next: names keeps the row with a mistake that refuses a save, until it is pointed elsewhere", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(
        request,
        bot,
        "orderValuation",
        {
            description: "D",
            firstStep: "a",
            steps: { a: { say: "A", next: "b" }, b: { say: "B", next: "c" }, c: { say: "C", next: "b" } },
        },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.getByRole("button", { name: "Delete step b" }).click();

    await expect(page.locator('[data-row="next"]').getByLabel("Next step")).toHaveValue("b");
    await expect(page.getByText("Refuses a save").first()).toBeVisible();
    await page.locator('[data-row="next"]').getByLabel("Next step").selectOption("a");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toHaveCount(0);
    const saved = await stored<{ steps: Record<string, { next?: string }> }>(request, bot, "orderValuation");
    expect(Object.keys(saved.steps)).toEqual(["a", "c"]);
    expect(saved.steps["a"]?.next).toBe("c");
    expect(saved.steps["c"]?.next).toBe("a");
});

test("AC-procedure-creator-7: a save on an old revision is refused, the page stays as it was, and Reload brings the stored procedure", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.getByLabel("Description").fill("Marta's description");
    await agentSaves(request, bot, "orderValuation", { ...AGENT_WROTE, description: "The agent's description" }, 1);

    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(page.getByRole("alert").first()).toContainText("it is now revision 2");
    await expect(page.getByLabel("Description")).toHaveValue("Marta's description");
    await page.getByRole("button", { name: "Reload" }).first().click();
    await expect(page.getByLabel("Description")).toHaveValue("The agent's description");
    await expect(page.getByRole("alert")).toHaveCount(0);
});

test("AC-procedure-creator-6: a save with only a Version mistake succeeds, and a save with a Save mistake is refused and the page stays", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.getByLabel("When stalled").fill("");
    await expect(page.getByText("Stops a version").first()).toBeVisible();

    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toBeHidden();
    expect((await stored<{ revision: number }>(request, bot, "orderValuation")).revision).toBe(2);

    await open(page, "mark");
    await page.getByLabel("Value of system.flag").fill("far too long");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(page.getByRole("alert")).toContainText("Your page is unchanged");
    await expect(page.getByLabel("Value of system.flag")).toHaveValue("far too long");
    await expect(page.getByText("Unsaved changes")).toBeVisible();
    const after = await stored<{ revision: number; steps: { mark: { assign: Record<string, string> } } }>(request, bot, "orderValuation");
    expect(after.revision).toBe(2);
    expect(after.steps.mark.assign).toEqual({ "system.flag": "big" });
});

test("AC-procedure-creator-17: leaving the page with unsaved changes asks first", async ({ page, request }) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.getByLabel("Description").fill("changed");
    const asked: string[] = [];
    page.once("dialog", (dialog) => {
        asked.push(dialog.message());
        void dialog.dismiss();
    });

    await page.getByRole("navigation", { name: "Bots" }).getByRole("link", { name: "Procedures" }).click();

    await expect.poll(() => asked.length).toBe(1);
    expect(asked[0]).toContain("unsaved changes");
    await expect(page.getByRole("heading", { name: "Procedure orderValuation" })).toBeVisible();
    page.once("dialog", (dialog) => void dialog.accept());
    await page.getByRole("navigation", { name: "Bots" }).getByRole("link", { name: "Procedures" }).click();
    await expect(page.getByRole("heading", { name: /Procedures of/ })).toBeVisible();
});

test("AC-procedure-creator-21: a start step is built with its form, which leaves out conversation, and reads back from the server", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(
        request,
        bot,
        "documentsList",
        { description: "Collects the documents", firstStep: "hello", steps: { hello: { say: "Hi" } } },
        0,
    );
    await agentSaves(request, bot, "orderValuation", { description: "The customer orders a valuation", firstStep: null, steps: {} }, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await addStep(page, "start");
    const target = page.getByLabel("Start this procedure");
    await expect(target.getByRole("option")).toHaveText(["(choose)", "documentsList"]);
    await target.selectOption("documentsList");
    await expect(row(page, "start")).toContainText("documentsList");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const saved = await stored<{ steps: Record<string, { start?: string }> }>(request, bot, "orderValuation");
        expect(saved.steps["start"]?.start).toBe("documentsList");
    }).toPass();
    await page.reload();
    await expect(page.getByLabel("Start this procedure")).toHaveValue("documentsList");
});

test("AC-procedure-creator-22: an inform step is built with its form and reads back from the server with its points in order", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", { description: "The customer orders a valuation", firstStep: null, steps: {} }, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await addStep(page, "inform");
    await page.getByLabel("Instruction").fill("Tell the customer what happens next.");
    await page.getByRole("button", { name: "Add point" }).click();
    await page.getByLabel("Name of point 1").fill("turnaround");
    await page.getByLabel("Text of point 1").fill("Ready within 7 working days.");
    await page.getByRole("button", { name: "Add point" }).click();
    await page.getByLabel("Name of point 2").fill("a");
    await page.getByLabel("Text of point 2").fill("Sent by e-mail.");
    await expect(row(page, "inform")).toContainText("2 points");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const raw = await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).text();
        const saved = JSON.parse(raw) as {
            steps: Record<string, { inform?: { instruction: string; points: Record<string, string> } }>;
        };
        expect(saved.steps["inform"]?.inform?.instruction).toBe("Tell the customer what happens next.");
        expect(Object.entries(saved.steps["inform"]?.inform?.points ?? {})).toEqual([
            ["turnaround", "Ready within 7 working days."],
            ["a", "Sent by e-mail."],
        ]);
    }).toPass();
    await page.reload();
    await expect(page.getByLabel("Name of point 1")).toHaveValue("turnaround");
    await expect(page.getByLabel("Text of point 2")).toHaveValue("Sent by e-mail.");
});

test("AC-procedure-creator-20: knowledge is edited on whenStalled and on an inform, by base, with articles and queries, and reads back from the server", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    for (const base of ["offer", "reports"]) {
        expect((await request.put(`/api/bots/${bot}/knowledge-bases/${base}?ifRevision=0`, { data: { title: base } })).status()).toBe(200);
    }
    for (const [id, knowledgeBase] of [
        ["officeContact", "offer"],
        ["reportContents", "reports"],
    ] as const) {
        expect(
            (
                await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, {
                    data: { knowledgeBase, title: id, content: "c", llmGuide: "g", description: "d" },
                })
            ).status(),
        ).toBe(200);
    }
    await agentSaves(
        request,
        bot,
        "orderValuation",
        {
            description: "The customer orders a valuation",
            firstStep: "tell",
            steps: { tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." } } } },
            whenStalled: { instruction: "Say you could not finish." },
        },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    const add = page.getByLabel("Add knowledge");
    await add.selectOption("offer");
    const stalled = page.locator('[data-place="knowledge:whenStalled:0"]');
    await expect(stalled.getByRole("checkbox")).toHaveCount(1);
    await stalled.getByLabel("officeContact (officeContact)").check();
    await stalled.getByRole("button", { name: "+ Add query" }).click();
    await stalled.getByLabel("Search query 1", { exact: true }).fill("how long a valuation takes");
    await expect(add.getByRole("option")).toHaveText(["+ Add knowledge…", "reports"]);

    await open(page, "tell");
    await page.locator('section[aria-label="Step tell"]').getByLabel("Add knowledge").selectOption("reports");
    const told = page.locator('[data-place="knowledge:tell:0"]');
    await told.getByRole("button", { name: "+ Add query" }).click();
    await told.getByLabel("Search query 1", { exact: true }).fill("opening hours");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const saved = await stored<{
            whenStalled: { knowledge: unknown };
            steps: Record<string, { inform?: { knowledge: unknown } }>;
        }>(request, bot, "orderValuation");
        expect(saved.whenStalled.knowledge).toEqual([
            { knowledgeBase: "offer", articles: ["officeContact"], searchQueries: ["how long a valuation takes"] },
        ]);
        expect(saved.steps["tell"]?.inform?.knowledge).toMatchObject([{ knowledgeBase: "reports", searchQueries: ["opening hours"] }]);
    }).toPass();
});

test("AC-procedure-creator-37: changing the base of an entry clears its articles and keeps its queries", async ({ page, request }) => {
    const bot = await newBot(request);
    for (const base of ["offer", "reports"]) {
        expect((await request.put(`/api/bots/${bot}/knowledge-bases/${base}?ifRevision=0`, { data: { title: base } })).status()).toBe(200);
    }
    for (const [id, knowledgeBase] of [
        ["officeContact", "offer"],
        ["reportContents", "reports"],
    ] as const) {
        expect(
            (
                await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, {
                    data: { knowledgeBase, title: id, content: "c", llmGuide: "g", description: "d" },
                })
            ).status(),
        ).toBe(200);
    }
    await agentSaves(
        request,
        bot,
        "orderValuation",
        {
            ...AGENT_WROTE,
            whenStalled: {
                instruction: "Say you could not finish.",
                knowledge: [{ knowledgeBase: "offer", articles: ["officeContact"], searchQueries: ["opening hours"] }],
            },
        },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    const entry = page.locator('[data-place="knowledge:whenStalled:0"]');
    await expect(entry.getByRole("checkbox", { name: /officeContact/ })).toBeChecked();

    await entry.getByLabel("Knowledge base of entry 1").selectOption("reports");

    await expect(entry.getByRole("checkbox")).toHaveCount(1);
    await expect(entry.getByRole("checkbox", { name: /reportContents/ })).not.toBeChecked();
    await expect(entry.getByLabel("Search query 1", { exact: true })).toHaveValue("opening hours");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect
        .poll(async () => {
            const saved = await stored<{ whenStalled: { knowledge: { articles?: string[] | null }[] } }>(request, bot, "orderValuation");
            const first = saved.whenStalled.knowledge[0];
            return { ...first, articles: first?.articles ?? [] };
        })
        .toEqual({ knowledgeBase: "reports", articles: [], searchQueries: ["opening hours"] });
});

test("AC-procedure-creator-18: the form adds a local fact, a step's form offers it, the save stores it, and the form removes it again", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", { description: "D", firstStep: "ask", steps: { ask: { collect: [] } } }, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    const panel = page.getByRole("region", { name: "Local facts" });
    await panel.getByRole("button", { name: "Add a local fact" }).click();
    await panel.getByLabel("Name").fill("visitDate");
    await panel.getByLabel("Maximum length").fill("20");
    await panel.getByLabel("Fact description").fill("When to visit");
    await panel.getByRole("button", { name: "Keep" }).click();
    await expect(panel).toContainText("text, up to 20 · set");

    await open(page, "ask");
    await page.getByRole("checkbox", { name: "visitDate" }).check();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toBeHidden();

    const saved = await stored<{
        localFacts: Record<string, { maxLength: number; description: string }>;
        steps: Record<string, { collect?: string[] }>;
    }>(request, bot, "orderValuation");
    expect(saved.localFacts["visitDate"]).toMatchObject({ maxLength: 20, description: "When to visit" });
    expect(saved.steps["ask"]?.collect).toEqual(["visitDate"]);
    const listed = (await (await request.get(`/api/bots/${bot}/facts`)).json()) as { name: string }[];
    expect(listed.map((fact) => fact.name)).not.toContain("visitDate");

    await page.getByRole("checkbox", { name: "visitDate" }).uncheck();
    await panel.getByRole("button", { name: "Edit visitDate" }).click();
    await panel.getByLabel("Maximum length").fill("30");
    await panel.getByRole("button", { name: "Keep" }).click();
    await expect(panel).toContainText("text, up to 30");
    await panel.getByRole("button", { name: "Remove visitDate" }).click();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toBeHidden();
    const after = await stored<{ localFacts: unknown }>(request, bot, "orderValuation");
    expect(after.localFacts ?? {}).toEqual({});
});

test("AC-resource-rename-2: Change id on the page lands on the new address", async ({ page, request }) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.locator("[data-step]")).toHaveCount(4);

    await page.getByRole("button", { name: "Change id" }).click();
    const dialog = page.getByRole("dialog", { name: "Change id" });
    await dialog.getByLabel("New id").fill("valuation");
    await expect(dialog).toContainText("This rewrites nothing else.");
    await dialog.getByRole("button", { name: "Change id" }).click();

    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/valuation/edit$`));
    await expect(page.getByRole("heading", { name: "Procedure valuation" })).toBeVisible();
});

test("AC-procedure-creator-19: the page offers only the blocks the bot last declared, and every block when it has declared none", async ({
    page,
    request,
}) => {
    const id = freshId();
    const server = createServer((_, response) => {
        response
            .writeHead(200, { "content-type": "application/json" })
            .end(JSON.stringify({ bot: id, formatVersion: 2, blocks: ["listen", "say"] }));
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    try {
        const address = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address } })).status()).toBe(201);
        await agentSaves(request, id, "conversation", CONVERSATION, 0);
        const blocks = page.getByLabel("Add step", { exact: true });

        await page.goto(`/bots/${id}/procedures/conversation`);
        await expect(blocks.getByRole("option", { name: "Ask" })).toHaveCount(1);
        await expect(page.getByText("nothing is being checked")).toBeVisible();

        expect((await request.get(`/api/bots/${id}/declaration`)).status()).toBe(200);
        await page.goto(`/bots/${id}/procedures/conversation`);
        await expect(blocks.getByRole("option", { name: "Say" })).toHaveCount(1);
        await expect(blocks.getByRole("option", { name: "Ask" })).toHaveCount(0);
        await expect(page.getByText("nothing is being checked")).toHaveCount(0);
    } finally {
        server.close();
    }
});

test("AC-procedure-creator-38: Knowledge shows on When stalled and on a Tell while the bot answers nothing, hides when it declares without knowledge, and shows when it declares knowledge", async ({
    page,
    request,
}) => {
    const id = freshId();
    let declared: object = { bot: id, formatVersion: 2, blocks: ["listen", "say", "inform"] };
    const server = createServer((_, response) => {
        response.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify(declared));
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    try {
        const address = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address } })).status()).toBe(201);
        await agentSaves(request, id, "conversation", CONVERSATION, 0);
        await agentSaves(
            request,
            id,
            "orderValuation",
            {
                description: "D",
                firstStep: "tell",
                steps: { tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." } } } },
                whenStalled: { instruction: "Say you could not finish." },
            },
            0,
        );
        const sectionsAre = async (count: number) => {
            await page.goto(`/bots/${id}/procedures/orderValuation`);
            await open(page, "tell");
            await expect(page.locator('section[aria-label="Step tell"], [data-place="field:whenStalled"]')).toHaveCount(2);
            await expect(page.getByRole("group", { name: "Knowledge", exact: true })).toHaveCount(count);
        };

        await sectionsAre(2);

        expect((await request.get(`/api/bots/${id}/declaration`)).status()).toBe(200);
        await sectionsAre(0);

        declared = { ...declared, knowledge: true };
        expect((await request.get(`/api/bots/${id}/declaration`)).status()).toBe(200);
        await sectionsAre(2);
    } finally {
        server.close();
    }
});

test("AC-action-block-16: an action step's form offers the bot's actions, then a row per parameter and result with only the facts that fit, and On failure as a branch; the mistakes show beside it", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/checkKind?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: { place: { type: "text", maxLength: 100, description: "Where" } },
            results: { kind: { type: "enum", values: ["house", "flat"], description: "What" } },
            implementation: { http: { method: "POST", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await agentSaves(request, bot, "withAction", { description: "Runs an action", firstStep: "run", steps: { run: { action: "" } } }, 0);
    await page.goto(`/bots/${bot}/procedures/withAction`);
    await open(page, "run");

    await page.getByLabel("Action", { exact: true }).selectOption("checkKind");

    const parameter = page.getByLabel("Parameters: place");
    await expect(parameter.getByRole("option")).toHaveText(["(choose a fact)", "customer.city", "system.flag"]);
    const result = page.getByLabel("Results: kind");
    await expect(result.getByRole("option")).toHaveText(["(ignored)", "order.kind"]);
    await expect(page.getByText("Step 'run' has no onFailure.").first()).toBeVisible();
    await expect(page.getByText("Step 'run' does not map parameter 'place'.").first()).toBeVisible();

    await parameter.selectOption("customer.city");
    await result.selectOption("order.kind");
    await page.getByRole("button", { name: "Set what happens" }).click();
    await addStep(page, "say", "Add step to On failure of run");

    await expect(page.getByText("Step 'run' has no onFailure.")).toHaveCount(0);
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect
        .poll(async () => (await stored<{ steps: { run: unknown } }>(request, bot, "withAction")).steps.run)
        .toMatchObject({ action: "checkKind", with: { place: "customer.city" }, results: { kind: "order.kind" }, onFailure: "say" });
});

test("AC-action-block-17: the Uses panel of a procedure lists the action and each fact a step maps, with the field holding each", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await request.put(`/api/bots/${bot}/actions/checkKind?ifRevision=0`, {
        data: {
            description: "Checks",
            parameters: { place: { type: "text", maxLength: 100, description: "Where" } },
            results: null,
            implementation: { http: { method: "POST", url: "https://x.internal", headers: null, timeoutSeconds: null } },
        },
    });
    await agentSaves(
        request,
        bot,
        "withAction",
        {
            description: "Runs",
            firstStep: "run",
            steps: { run: { action: "checkKind", with: { place: "customer.city" }, onFailure: "failed" }, failed: { say: "S" } },
        },
        0,
    );

    await page.goto(`/bots/${bot}/procedures/withAction`);

    const uses = page.getByRole("region", { name: "Uses" });
    await expect(uses).toContainText("checkKind");
    await expect(uses).toContainText("steps.run.action");
    await expect(uses).toContainText("steps.run.with.place");
});

test("AC-fact-pattern-11: the local fact form sets a pattern, the list of mistakes names a bad one, the save stores a good one, and clearing it takes it off", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", { description: "D", firstStep: "ask", steps: { ask: { collect: [] } } }, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    const panel = page.getByRole("region", { name: "Local facts" });
    await panel.getByRole("button", { name: "Add a local fact" }).click();
    await panel.getByLabel("Name").fill("visitCode");
    await panel.getByLabel("Maximum length").fill("20");
    await panel.getByLabel("Fact description").fill("The code");
    await panel.getByLabel("Pattern: regular expression").fill("(?=a)b");
    await panel.getByLabel("Pattern: what it means").fill("two capitals");
    await panel.getByRole("button", { name: "Keep" }).click();
    await expect(page.getByText(/The pattern of 'visitCode' cannot be used/).first()).toBeVisible();

    await panel.getByRole("button", { name: "Edit visitCode" }).click();
    await expect(panel.getByLabel("Pattern: regular expression")).toHaveValue("(?=a)b");
    await panel.getByLabel("Pattern: regular expression").fill("[A-Z]{2}");
    await panel.getByLabel("Pattern: example").fill("AB");
    await panel.getByLabel("Pattern: tell the customer").selectOption("afterRefusal");
    await panel.getByRole("button", { name: "Keep" }).click();
    await expect(panel).toContainText("text, up to 20, pattern [A-Z]{2}");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toBeHidden();
    const read = async () =>
        (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
            localFacts: Record<string, { pattern?: unknown }>;
        };
    expect((await read()).localFacts["visitCode"]?.pattern).toEqual({
        regex: "[A-Z]{2}",
        means: "two capitals",
        example: "AB",
        tellCustomer: "afterRefusal",
    });

    await panel.getByRole("button", { name: "Edit visitCode" }).click();
    await panel.getByLabel("Pattern: regular expression").fill("");
    await panel.getByLabel("Pattern: what it means").fill("");
    await panel.getByLabel("Pattern: example").fill("");
    await panel.getByRole("button", { name: "Keep" }).click();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Unsaved changes")).toBeHidden();
    expect((await read()).localFacts["visitCode"]?.pattern ?? null).toBeNull();
});

test("AC-chatbot-contract-6: when interrupted is shown and set on a procedure, and offered on none for conversation", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", { ...AGENT_WROTE, whenInterrupted: "discard" }, 0);

    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.getByLabel("When interrupted")).toHaveValue("discard");

    await page.getByLabel("When interrupted").selectOption("keep");
    await page.getByRole("button", { name: "Save" }).click();

    await expect
        .poll(async () => {
            const read = await request.get(`/api/bots/${bot}/procedures/orderValuation`);
            return ((await read.json()) as { whenInterrupted: string | null }).whenInterrupted;
        })
        .toBe("keep");

    await page.goto(`/bots/${bot}/procedures/conversation`);
    await expect(page.getByLabel("When stalled")).toBeVisible();
    await expect(page.getByLabel("When interrupted")).toHaveCount(0);
});

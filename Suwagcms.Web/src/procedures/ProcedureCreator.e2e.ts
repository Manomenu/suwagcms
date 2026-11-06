// A person draws a procedure in the browser: the graph, the forms, the live list of mistakes, and the save.
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
    // Why: a bot with no `conversation` always has that one mistake, which would hide whether the drawing is ready.
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

function node(page: Page, name: string) {
    return page.locator(`[data-step="${name}"]`);
}

async function boxOf(page: Page, name: string) {
    const box = await node(page, name).boundingBox();
    if (box === null) throw new Error(`${name} is not on screen`);
    return box;
}

// Why: React Flow starts a connection on mouse down on a handle, needs moves in between, and ends it over the target's handle.
async function drawEdge(page: Page, from: string, to: string): Promise<void> {
    await page.locator(".react-flow__controls-fitview").click();
    const handle = await node(page, from).locator(".react-flow__handle.source").boundingBox();
    const target = await node(page, to).locator(".react-flow__handle.target").boundingBox();
    if (handle === null || target === null) throw new Error(`${from} or ${to} has no handle to draw between`);
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 12 });
    await page.mouse.up();
}

test("AC-procedure-creator-1: an author builds a procedure with the mouse and the forms, with rules and a loop, and saves it", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/procedures`);
    await page.getByLabel("Id").fill("orderValuation");
    await page.getByLabel("Description").fill("The customer orders a valuation of a property.");
    await page.getByRole("button", { name: "Create procedure" }).click();
    await expect(page.getByRole("heading", { name: /Creator: orderValuation/ })).toBeVisible();
    await expect(page.getByText("'orderValuation' has no firstStep.")).toBeVisible();

    const blocks = page.getByRole("complementary", { name: "Blocks" });
    await blocks.getByRole("button", { name: "listen", exact: true }).click();
    await page.getByLabel("First step").check();
    await blocks.getByRole("button", { name: "collect", exact: true }).click();
    await page.getByLabel("customer.city").check();
    await page.getByLabel("order.kind").check();
    await expect(page.getByLabel("system.flag")).toHaveCount(0);
    await blocks.getByRole("button", { name: "assign", exact: true }).click();
    await page.getByLabel("Assign another fact").selectOption("system.flag");
    await page.getByLabel("Value of system.flag").fill("big");
    await blocks.getByRole("button", { name: "say", exact: true }).click();
    await page.getByLabel("Say", { exact: true }).fill("Thank you, we will call you.");
    await blocks.getByRole("button", { name: "say", exact: true }).click();
    await page.getByLabel("Say", { exact: true }).fill("Sorry, we only value houses.");
    await expect(node(page, "say2")).toBeVisible();

    // The edges are drawn with the mouse: from a step's handle onto the step it leads to.
    await drawEdge(page, "listen", "collect");
    await drawEdge(page, "assign", "say");
    await drawEdge(page, "say", "listen");

    // The rule of collect: when order.kind is house, go to assign; otherwise say2. The form already holds the default rule.
    await node(page, "collect").click();
    await page.getByRole("button", { name: "Add rule" }).click();
    await page.getByRole("button", { name: "Add a condition" }).click();
    await page.getByLabel("Fact", { exact: true }).selectOption("order.kind");
    await page.getByRole("checkbox", { name: "house" }).check();
    await page.locator('[data-rule="0"]').getByLabel("Go to").selectOption("assign");
    await page.locator('[data-rule="default"]').getByLabel("Otherwise go to").selectOption("say2");

    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await page.getByLabel("When stalled").fill("Tell the customer you could not finish.");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();

    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByText("unsaved changes")).toHaveCount(0);
    const saved = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
        firstStep: string;
        revision: number;
        steps: Record<string, Record<string, unknown>>;
        notReady: unknown[];
    };
    expect(saved).toMatchObject({ firstStep: "listen", revision: 2, notReady: [] });
    expect(saved.steps["listen"]).toMatchObject({ next: "collect" });
    expect(saved.steps["collect"]).toMatchObject({
        collect: ["customer.city", "order.kind"],
        next: [{ when: { "order.kind": { oneOf: ["house"] } }, to: "assign" }, { to: "say2" }],
    });
    expect(saved.steps["assign"]).toMatchObject({ assign: { "system.flag": "big" }, next: "say" });
    expect(saved.steps["say"]).toMatchObject({ say: "Thank you, we will call you.", next: "listen" });
    expect(saved.steps["say2"]).toMatchObject({ say: "Sorry, we only value houses." });
});

test("AC-procedure-creator-5: removing the only edge into a step shows it unreachable, and AC-procedure-creator-4: clicking the mistake selects the step", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.locator("[data-step]")).toHaveCount(4);
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();

    await page.locator('[data-id="mark:next"]').click({ force: true });
    await page.keyboard.press("Backspace");

    const mistake = page.getByRole("button", { name: /No path from firstStep reaches step 'confirm'/ });
    await expect(mistake).toBeVisible();
    await expect(mistake).toContainText("Stops a version");
    await expect(mistake).toContainText("Connect it from the next of a step that is reached, or delete it.");
    await mistake.click();
    await expect(page.getByRole("region", { name: "Step confirm" })).toBeVisible();
    await expect(node(page, "confirm")).toBeInViewport();

    // AC-procedure-creator-3: the mistake goes when the change is undone by hand.
    await node(page, "mark").click();
    await page.getByLabel("Go to").selectOption("confirm");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();
});

test("AC-procedure-creator-3: a mistake of the drawing appears without saving, with its fix, and nothing was saved", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await page.getByLabel("When stalled").fill("");

    await expect(page.getByRole("button", { name: /whenStalled/ })).toBeVisible();
    await page.getByLabel("When stalled").fill("Tell the customer you could not finish.");
    await expect(page.getByText("No mistakes: ready for a version.")).toBeVisible();
    const stored = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as { revision: number };
    expect(stored.revision).toBe(1);
});

test("AC-procedure-creator-2: a procedure an agent wrote opens laid out, AC-procedure-creator-12: a moved node stays where it was left and the revision stays, AC-procedure-creator-13: arrange lays out the whole graph", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    expect((await (await request.get(`/api/bots/${bot}/procedures/orderValuation/layout`)).json()) as object).toEqual({ positions: {} });
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.locator("[data-step]")).toHaveCount(4);

    const before = await boxOf(page, "mark");
    const grip = await node(page, "mark").locator("div").first().boundingBox();
    if (grip === null) throw new Error("no header to drag");
    await page.mouse.move(grip.x + 20, grip.y + 8);
    await page.mouse.down();
    await page.mouse.move(grip.x + 20 + 40, grip.y + 8 + 120, { steps: 8 });
    await page.mouse.up();
    await expect
        .poll(async () =>
            Object.keys(
                ((await (await request.get(`/api/bots/${bot}/procedures/orderValuation/layout`)).json()) as { positions: object })
                    .positions,
            ),
        )
        .toContain("mark");
    const moved = await boxOf(page, "mark");
    const ask0 = await boxOf(page, "ask");
    expect(Math.round(moved.y - before.y)).toBeGreaterThan(80);

    const stored = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as { revision: number };
    expect(stored.revision).toBe(1);
    await page.reload();
    await expect(page.locator("[data-step]")).toHaveCount(4);
    const reloaded = await boxOf(page, "mark");
    const askAfter = await boxOf(page, "ask");
    expect(Math.abs(reloaded.y - askAfter.y - (moved.y - ask0.y))).toBeLessThan(2);

    await page.getByRole("button", { name: "Arrange" }).click();
    await expect
        .poll(async () =>
            Object.keys(
                ((await (await request.get(`/api/bots/${bot}/procedures/orderValuation/layout`)).json()) as { positions: object })
                    .positions,
            ).sort(),
        )
        .toEqual(["ask", "confirm", "goodbye", "mark"]);
});

test("AC-procedure-creator-7: a save on an old revision is refused, the drawing stays on screen, and Reload brings the stored procedure", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await page.getByLabel("Description").fill("Marta's description");
    await agentSaves(request, bot, "orderValuation", { ...AGENT_WROTE, description: "The agent's description" }, 1);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel("Description")).toHaveValue("Marta's description");
    await page.getByRole("button", { name: "Reload" }).click();
    await expect(page.getByLabel("Description")).toHaveValue("The agent's description");
    await expect(page.getByRole("alert")).toHaveCount(0);
});

test("AC-procedure-creator-6: a save with only a Version mistake succeeds, and a save with a Save mistake is refused and the drawing stays", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await page.getByLabel("When stalled").fill("");
    await expect(page.getByRole("button", { name: /whenStalled/ })).toContainText("Stops a version");

    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("unsaved changes")).toBeHidden();
    const versionOnly = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as { revision: number };
    expect(versionOnly.revision).toBe(2);

    await node(page, "mark").click();
    await page.getByLabel("Value of system.flag").fill("far too long");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(page.getByRole("alert")).toContainText("Your drawing is unchanged");
    await expect(page.getByLabel("Value of system.flag")).toHaveValue("far too long");
    await expect(page.getByText("unsaved changes")).toBeVisible();
    const stored = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
        revision: number;
        steps: { mark: { assign: Record<string, string> } };
    };
    expect(stored.revision).toBe(2);
    expect(stored.steps.mark.assign).toEqual({ "system.flag": "big" });
});

test("AC-procedure-creator-17: leaving the creator with unsaved changes asks first", async ({ page, request }) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await page.getByLabel("Description").fill("changed");
    const asked: string[] = [];
    page.once("dialog", (dialog) => {
        asked.push(dialog.message());
        void dialog.dismiss();
    });

    await page.getByRole("navigation", { name: "Bots" }).getByRole("link", { name: "Procedures" }).click();

    await expect.poll(() => asked.length).toBe(1);
    expect(asked[0]).toContain("unsaved changes");
    await expect(page.getByRole("heading", { name: /Creator: orderValuation/ })).toBeVisible();
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

    await page.getByRole("complementary", { name: "Blocks" }).getByRole("button", { name: "start" }).click();
    await node(page, "start").click();
    const target = page.getByLabel("Start this procedure");
    await expect(target.getByRole("option")).toHaveText(["(choose)", "documentsList"]);
    await target.selectOption("documentsList");
    await expect(node(page, "start")).toContainText("start: documentsList");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const stored = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
            steps: Record<string, { start?: string }>;
        };
        expect(stored.steps["start"]?.start).toBe("documentsList");
    }).toPass();
    await page.reload();
    await node(page, "start").click();
    await expect(page.getByLabel("Start this procedure")).toHaveValue("documentsList");
});

test("AC-procedure-creator-22: an inform step is built with its form and reads back from the server with its points in order", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", { description: "The customer orders a valuation", firstStep: null, steps: {} }, 0);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);

    await page.getByRole("complementary", { name: "Blocks" }).getByRole("button", { name: "inform" }).click();
    await node(page, "inform").click();
    await page.getByLabel("Instruction").fill("Tell the customer what happens next.");
    await page.getByRole("button", { name: "Add point" }).click();
    await page.getByLabel("Name of point 1").fill("turnaround");
    await page.getByLabel("Text of point 1").fill("Ready within 7 working days.");
    await page.getByRole("button", { name: "Add point" }).click();
    await page.getByLabel("Name of point 2").fill("a");
    await page.getByLabel("Text of point 2").fill("Sent by e-mail.");
    await expect(node(page, "inform")).toContainText("inform: turnaround, a");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const raw = await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).text();
        const stored = JSON.parse(raw) as {
            steps: Record<string, { inform?: { instruction: string; points: Record<string, string> } }>;
        };
        expect(stored.steps["inform"]?.inform?.instruction).toBe("Tell the customer what happens next.");
        expect(Object.entries(stored.steps["inform"]?.inform?.points ?? {})).toEqual([
            ["turnaround", "Ready within 7 working days."],
            ["a", "Sent by e-mail."],
        ]);
    }).toPass();
    await page.reload();
    await node(page, "inform").click();
    await expect(page.getByLabel("Name of point 1")).toHaveValue("turnaround");
    await expect(page.getByLabel("Text of point 2")).toHaveValue("Sent by e-mail.");
});

test("AC-procedure-creator-20: a context is edited on whenStalled and on an inform, with articles and queries, and reads back from the server", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    expect((await request.put(`/api/bots/${bot}/knowledge-bases/offer?ifRevision=0`, { data: { title: "Offer" } })).status()).toBe(200);
    expect(
        (
            await request.put(`/api/bots/${bot}/articles/officeContact?ifRevision=0`, {
                data: { knowledgeBase: "offer", title: "The office", content: "c", llmGuide: "g", description: "d" },
            })
        ).status(),
    ).toBe(200);
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

    await page.getByRole("group", { name: "Articles to read" }).getByLabel("The office (officeContact)").check();
    await page.getByRole("button", { name: "Add search query" }).click();
    await page.getByLabel("Search query 1", { exact: true }).fill("how long a valuation takes");
    await node(page, "tell").click();
    await page.getByRole("button", { name: "Add search query" }).click();
    await page.getByLabel("Search query 1", { exact: true }).fill("opening hours");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(async () => {
        const stored = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
            whenStalled: { context: unknown };
            steps: Record<string, { inform?: { context: unknown } }>;
        };
        expect(stored.whenStalled.context).toEqual({ articles: ["officeContact"], searchQueries: ["how long a valuation takes"] });
        expect(stored.steps["tell"]?.inform?.context).toMatchObject({ searchQueries: ["opening hours"] });
    }).toPass();
});

test("AC-procedure-creator-18: the panel adds a local fact, a step's form offers it, the save stores it, and the panel removes it again", async ({
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

    await node(page, "ask").click();
    await page.getByRole("checkbox", { name: "visitDate" }).check();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("unsaved changes")).toBeHidden();

    const saved = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as {
        localFacts: Record<string, { maxLength: number; description: string }>;
        steps: Record<string, { collect?: string[] }>;
    };
    expect(saved.localFacts["visitDate"]).toMatchObject({ maxLength: 20, description: "When to visit" });
    expect(saved.steps["ask"]?.collect).toEqual(["visitDate"]);
    const listed = (await (await request.get(`/api/bots/${bot}/facts`)).json()) as { name: string }[];
    expect(listed.map((fact) => fact.name)).not.toContain("visitDate");

    await page.getByRole("checkbox", { name: "visitDate" }).uncheck();
    await page.locator(".react-flow__pane").click({ position: { x: 5, y: 5 } });
    await panel.getByRole("button", { name: "Edit visitDate" }).click();
    await panel.getByLabel("Maximum length").fill("30");
    await panel.getByRole("button", { name: "Keep" }).click();
    await expect(panel).toContainText("text, up to 30");
    await panel.getByRole("button", { name: "Remove visitDate" }).click();
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("unsaved changes")).toBeHidden();
    const after = (await (await request.get(`/api/bots/${bot}/procedures/orderValuation`)).json()) as { localFacts: unknown };
    expect(after.localFacts ?? {}).toEqual({});
});

test("AC-resource-rename-2: Change id in the creator lands on the new address, and the node positions stay", async ({ page, request }) => {
    const bot = await newBot(request);
    await agentSaves(request, bot, "orderValuation", AGENT_WROTE, 0);
    const positions = { ask: { x: 11, y: 22 }, mark: { x: 33, y: 44 }, confirm: { x: 55, y: 66 }, goodbye: { x: 77, y: 88 } };
    expect((await request.put(`/api/bots/${bot}/procedures/orderValuation/layout`, { data: { positions } })).status()).toBe(200);
    await page.goto(`/bots/${bot}/procedures/orderValuation`);
    await expect(page.locator("[data-step]")).toHaveCount(4);

    await page.getByRole("button", { name: "Change id" }).click();
    const dialog = page.getByRole("dialog", { name: "Change id" });
    await dialog.getByLabel("New id").fill("valuation");
    await expect(dialog).toContainText("This rewrites nothing else.");
    await dialog.getByRole("button", { name: "Change id" }).click();

    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/valuation$`));
    await expect(page.getByRole("heading", { name: /Creator: valuation/ })).toBeVisible();
    const layout = (await (await request.get(`/api/bots/${bot}/procedures/valuation/layout`)).json()) as { positions: unknown };
    expect(layout.positions).toEqual(positions);
});

test("AC-procedure-creator-19: the creator offers only the blocks the bot last declared, and every block when it has declared none", async ({
    page,
    request,
}) => {
    const id = freshId();
    const server = createServer((_, response) => {
        response
            .writeHead(200, { "content-type": "application/json" })
            .end(JSON.stringify({ bot: id, formatVersion: 1, blocks: ["listen", "say"] }));
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    try {
        const address = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
        expect((await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address } })).status()).toBe(201);
        await agentSaves(request, id, "conversation", CONVERSATION, 0);
        const blocks = page.getByRole("complementary", { name: "Blocks" });

        await page.goto(`/bots/${id}/procedures/conversation`);
        await expect(blocks.getByRole("button", { name: "collect" })).toBeVisible();
        await expect(blocks.getByText("nothing is being checked")).toBeVisible();

        expect((await request.get(`/api/bots/${id}/declaration`)).status()).toBe(200);
        await page.goto(`/bots/${id}/procedures/conversation`);
        await expect(blocks.getByRole("button", { name: "say" })).toBeVisible();
        await expect(blocks.getByRole("button", { name: "collect" })).toHaveCount(0);
        await expect(blocks.getByText("nothing is being checked")).toHaveCount(0);
    } finally {
        server.close();
    }
});

test("AC-action-block-16: an action step's form offers the bot's actions, then a row per parameter and result with only the facts that fit, and a failure edge; the mistakes show in the live list", async ({
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
    await agentSaves(
        request,
        bot,
        "withAction",
        { description: "Runs an action", firstStep: "run", steps: { run: { action: "" }, failed: { say: "Sorry" } } },
        0,
    );
    await page.goto(`/bots/${bot}/procedures/withAction`);
    await page.locator('[data-step="run"]').click();

    await page.getByLabel("Action", { exact: true }).selectOption("checkKind");

    const parameter = page.getByLabel("Parameters: place");
    await expect(parameter.getByRole("option")).toHaveText(["(choose a fact)", "customer.city", "system.flag"]);
    const result = page.getByLabel("Results: kind");
    await expect(result.getByRole("option")).toHaveText(["(ignored)", "order.kind"]);
    await expect(page.getByText("Step 'run' has no onFailure.")).toBeVisible();
    await expect(page.getByText("Step 'run' does not map parameter 'place'.")).toBeVisible();

    await parameter.selectOption("customer.city");
    await result.selectOption("order.kind");
    await page.getByLabel("On failure, go to").selectOption("failed");

    await expect(page.getByText("Step 'run' has no onFailure.")).toHaveCount(0);
    await expect(page.locator(".react-flow__edge")).toHaveCount(1);
    await page.getByRole("button", { name: "Save" }).click();
    await expect
        .poll(
            async () =>
                ((await (await request.get(`/api/bots/${bot}/procedures/withAction`)).json()) as { steps: { run: unknown } }).steps.run,
        )
        .toMatchObject({ action: "checkKind", with: { place: "customer.city" }, results: { kind: "order.kind" }, onFailure: "failed" });
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
    await expect(page.getByText("unsaved changes")).toBeHidden();
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
    await expect(page.getByText("unsaved changes")).toBeHidden();
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

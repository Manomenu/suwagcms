// A person turns an article off and on in the browser, against a bot that answers as the SDK does, or that cannot be reached.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { createServer, type Server } from "node:http";
import type { AddressInfo } from "node:net";
import { testBots } from "../../e2e/bots";

const { freshId } = testBots("avail");

/** Answers the state and availability routes the way the SDK does, holding the last set it was told. */
async function startBot(id: string): Promise<{ server: Server; address: string }> {
    let availability: unknown = { number: 0, off: [] };
    const server = createServer((request, response) => {
        const chunks: Buffer[] = [];
        request.on("data", (chunk: Buffer) => chunks.push(chunk));
        request.on("end", () => {
            if (request.method === "PUT") availability = JSON.parse(Buffer.concat(chunks).toString()) as unknown;
            response.setHeader("content-type", "application/json");
            response.end(JSON.stringify({ bot: id, current: null, held: [], inUse: [], storeError: null, availability }));
        });
    });
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    return { server, address: `http://127.0.0.1:${(server.address() as AddressInfo).port}` };
}

async function fill(request: APIRequestContext, bot: string, address: string): Promise<void> {
    expect((await request.post("/api/bots", { data: { id: bot, name: `Name of ${bot}`, address } })).status()).toBe(201);
    expect((await request.put(`/api/bots/${bot}/knowledge-bases/offer?ifRevision=0`, { data: { title: "Offer" } })).status()).toBe(200);
    for (const [id, content] of [
        ["cennik", "What a valuation costs."],
        ["rabaty", "See [the price list](cms://article/cennik)."],
    ] as const) {
        const response = await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, {
            data: { knowledgeBase: "offer", title: id, content, llmGuide: "Guide.", description: "What it says" },
        });
        expect(response.status()).toBe(200);
    }
}

test("AC-availability-1: an article turns off and on in the web, its screen and the list follow; AC-availability-2: the question shows the Used by list; AC-availability-9: the bot's answer confirms it", async ({
    page,
    request,
}) => {
    const bot = freshId();
    const { server, address } = await startBot(bot);
    try {
        await fill(request, bot, address);
        await page.goto(`/bots/${bot}/articles`);
        const row = page.getByRole("row").filter({ hasText: "cennik" }).first();
        await expect(row).toContainText("on");
        await row.getByRole("button", { name: "Edit" }).click();

        const panel = page.getByRole("region", { name: "Used by" }).first();
        await expect(panel).toContainText("rabaty");
        await page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn off" }).click();
        const dialog = page.getByRole("dialog", { name: "Turn off" });
        await expect(dialog).toContainText("rabaty");
        await expect(dialog).toContainText("article · content");
        await dialog.getByRole("button", { name: "Turn off" }).click();

        const group = page.getByRole("group", { name: "Availability" });
        await expect(group).toContainText("off");
        await expect(group).toContainText("Bot confirmed");
        await page.getByRole("button", { name: "Back to articles" }).click();
        const listed = page.getByRole("row").filter({ hasText: "cennik" }).first();
        await expect(listed).toContainText("off");
        await listed.getByRole("button", { name: "Edit" }).click();

        const state = (await (await fetch(`${address}/suwagcms/state`)).json()) as { availability: { off: { id: string }[] } };
        expect(state.availability.off.map((item) => item.id)).toEqual(["cennik"]);

        await group.getByRole("button", { name: "Turn on" }).click();
        await expect(page.getByRole("dialog", { name: "Turn off" })).toHaveCount(0);
        await expect(group.getByRole("button", { name: "Turn off" })).toBeVisible();
    } finally {
        server.close();
    }
});

test("AC-availability-16: with the bot unreachable the page says the change is saved and not delivered, with the reason, and sends again", async ({
    page,
    request,
}) => {
    const bot = freshId();
    const { server, address } = await startBot(bot);
    await fill(request, bot, address);
    await new Promise((resolve) => server.close(resolve));
    server.closeAllConnections();

    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "rabaty" }).first().getByRole("button", { name: "Edit" }).click();
    await page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn off" }).click();
    await page.getByRole("dialog", { name: "Turn off" }).getByRole("button", { name: "Turn off" }).click();

    await expect(page.getByRole("group", { name: "Availability" })).toContainText("off");
    const notice = page.getByRole("alert").filter({ hasText: "Not delivered." });
    await expect(notice).toContainText(`cannot reach ${address}`);
    await notice.getByRole("button", { name: "Send again" }).click();
    await expect(notice).toContainText(`cannot reach ${address}`);
    const view = (await (await request.get(`/api/bots/${bot}/availability?ask=false`)).json()) as {
        resources: { id: string; off: boolean }[];
    };
    expect(view.resources.find((item) => item.id === "rabaty")?.off).toBe(true);
});

test("AC-availability-22: a procedure turns off on its page and the list of procedures says so", async ({ page, request }) => {
    const bot = freshId();
    const { server, address } = await startBot(bot);
    try {
        await fill(request, bot, address);
        const response = await request.put(`/api/bots/${bot}/procedures/orderValuation?ifRevision=0`, {
            data: { description: "The customer orders a valuation" },
        });
        expect(response.status()).toBe(200);

        await page.goto(`/bots/${bot}/procedures/orderValuation`);
        await page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn off" }).click();
        await page.getByRole("dialog", { name: "Turn off" }).getByRole("button", { name: "Turn off" }).click();
        await expect(page.getByRole("group", { name: "Availability" })).toContainText("Bot confirmed");

        await page.goto(`/bots/${bot}/procedures`);
        await expect(page.getByRole("row").filter({ hasText: "orderValuation" })).toContainText("off");
    } finally {
        server.close();
    }
});

test("AC-availability-31: an article that is off still shows off after Change id", async ({ page, request }) => {
    const bot = freshId();
    const { server, address } = await startBot(bot);
    try {
        await fill(request, bot, address);
        await page.goto(`/bots/${bot}/articles?open=rabaty`);
        await page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn off" }).click();
        await page.getByRole("dialog", { name: "Turn off" }).getByRole("button", { name: "Turn off" }).click();
        await expect(page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn on" })).toBeVisible();

        await page.getByRole("button", { name: "Change id" }).click();
        const dialog = page.getByRole("dialog", { name: "Change id" });
        await dialog.getByLabel("New id").fill("discounts");
        await expect(dialog).toContainText("This rewrites nothing else.");
        await dialog.getByRole("button", { name: "Change id" }).click();

        await expect(page.getByLabel("Id", { exact: true })).toHaveValue("discounts");
        await expect(page.getByRole("group", { name: "Availability" }).getByRole("button", { name: "Turn on" })).toBeVisible();
    } finally {
        server.close();
    }
});

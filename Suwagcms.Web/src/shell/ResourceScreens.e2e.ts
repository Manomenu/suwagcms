// A person moves between a resource's list and its edit screen: the address says which one is open, on every kind.
import { expect, test, type APIRequestContext } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("screens");

const FACT = { type: "text", description: "The email", writableBy: null, values: null, min: null, max: null, maxLength: 80 };

async function addFact(request: APIRequestContext, bot: string, name: string): Promise<void> {
    expect((await request.put(`/api/bots/${bot}/facts/${name}?ifRevision=0`, { data: FACT })).status()).toBe(200);
}

async function addBase(request: APIRequestContext, bot: string, id: string): Promise<void> {
    expect(
        (
            await request.put(`/api/bots/${bot}/knowledge-bases/${id}?ifRevision=0`, {
                data: { title: id, embedding: "default", results: 5 },
            })
        ).status(),
    ).toBe(200);
}

async function addArticle(request: APIRequestContext, bot: string, id: string, knowledgeBase: string): Promise<void> {
    const data = { knowledgeBase, title: id, content: "c", llmGuide: "g", description: "d" };
    expect((await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, { data })).status()).toBe(200);
}

async function addProcedure(request: APIRequestContext, bot: string, id: string): Promise<void> {
    const data = { description: "A procedure", firstStep: null, steps: {}, whenStalled: null };
    expect((await request.put(`/api/bots/${bot}/procedures/${id}?ifRevision=0`, { data })).status()).toBe(200);
}

test("AC-web-shell-8, AC-web-shell-9: an id with dots opens at its address, survives a reload, and Back returns to the list", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addFact(request, bot, "customer.email");
    await page.goto(`/bots/${bot}/facts`);

    await page.getByRole("row").filter({ hasText: "customer.email" }).getByRole("button", { name: "Edit" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts/customer.email/edit`);
    await expect(page.getByLabel("Description")).toHaveValue("The email");

    await page.reload();
    await expect(page.getByRole("heading", { name: "Fact customer.email" })).toBeVisible();
    await expect(page.getByLabel("Description")).toHaveValue("The email");

    await page.goto(`/bots/${bot}/facts`);
    await page.goto(`/bots/${bot}/facts/customer.email/edit`);
    await page.goBack();
    await expect(page.getByRole("row").filter({ hasText: "customer.email" })).toBeVisible();
});

test("AC-web-shell-9: an unknown id says it is not found, with the way back, on a list screen and on a procedure", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);

    await page.goto(`/bots/${bot}/facts/no.such/edit`);
    await expect(page.getByRole("alert")).toContainText("no fact 'no.such'");
    await page.getByRole("button", { name: "Back to facts" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts`);

    await page.goto(`/bots/${bot}/procedures/noSuch/edit`);
    await expect(page.getByRole("alert")).toContainText("no procedure 'noSuch'");
    await page.getByRole("button", { name: "Back to procedures" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/procedures`);
});

test("AC-web-shell-10: the older addresses lead to the edit address", async ({ page, request }) => {
    const bot = await newBot(request);
    await addFact(request, bot, "customer.email");
    await addProcedure(request, bot, "refund");

    await page.goto(`/bots/${bot}/facts?open=customer.email`);
    await expect(page).toHaveURL(`/bots/${bot}/facts/customer.email/edit`);
    await expect(page.getByLabel("Description")).toHaveValue("The email");

    await page.goto(`/bots/${bot}/procedures/refund`);
    await expect(page).toHaveURL(`/bots/${bot}/procedures/refund/edit`);
    await expect(page.getByRole("heading", { name: "Procedure refund" })).toBeVisible();
});

test("AC-web-shell-11: adding or editing shows only that item, with a way back, on every kind", async ({ page, request }) => {
    const bot = await newBot(request);
    await addFact(request, bot, "customer.email");
    await addBase(request, bot, "offer");
    await addArticle(request, bot, "priceList", "offer");
    const screens = [
        { collection: "facts", id: "customer.email", add: "Add a fact", back: "Back to facts" },
        { collection: "knowledge-bases", id: "offer", add: "Add a knowledge base", back: "Back to knowledge bases" },
        { collection: "articles", id: "priceList", add: "Add an article", back: "Back to articles" },
    ];
    for (const { collection, id, add, back } of screens) {
        await page.goto(`/bots/${bot}/${collection}`);
        await expect(page.getByRole("button", { name: add })).toBeVisible();
        await expect(page.getByRole("table")).toBeVisible();

        await page.goto(`/bots/${bot}/${collection}/${id}/edit`);
        await expect(page.getByRole("button", { name: back })).toBeVisible();
        await expect(page.getByRole("table")).toHaveCount(0);
        await expect(page.getByRole("button", { name: add })).toHaveCount(0);

        await page.goto(`/bots/${bot}/${collection}/new`);
        await expect(page.getByRole("heading", { name: add })).toBeVisible();
        await expect(page.getByRole("table")).toHaveCount(0);
        await page.getByRole("button", { name: back }).click();
        await expect(page).toHaveURL(`/bots/${bot}/${collection}`);
    }
    await page.goto(`/bots/${bot}/actions/new`);
    await expect(page.getByRole("button", { name: "Back to actions" })).toBeVisible();
    await page.goto(`/bots/${bot}/procedures/new`);
    await expect(page.getByRole("button", { name: "Back to procedures" })).toBeVisible();
    await expect(page.getByRole("table")).toHaveCount(0);
});

test("AC-web-shell-13: the help opens on hover and on focus, and Escape closes it", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/facts/new`);
    const help = page.getByRole("button", { name: "About Writable by" });
    const tip = page.getByRole("tooltip");

    await help.hover();
    await expect(tip).toContainText("Who may write the fact");
    // Why: Radix keeps the help open while the pointer may be on its way to it, until a move lands outside that way.
    await page.mouse.move(0, 0, { steps: 15 });
    await expect(tip).toHaveCount(0);

    await help.focus();
    await expect(tip).toContainText("Who may write the fact");
    await page.keyboard.press("Escape");
    await expect(tip).toHaveCount(0);
});

test.describe("on a touch screen", () => {
    test.use({ hasTouch: true });

    test("AC-web-shell-13: a tap opens the help", async ({ page, request }) => {
        const bot = await newBot(request);
        await page.goto(`/bots/${bot}/facts/new`);

        await page.getByRole("button", { name: "About Writable by" }).tap();

        await expect(page.getByRole("tooltip")).toContainText("Who may write the fact");
    });
});

test("AC-web-shell-15: the article filters survive a reload and opening an article; the selected step survives a reload", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "offer");
    await addArticle(request, bot, "priceList", "offer");
    await addArticle(request, bot, "terms", "offer");
    await page.goto(`/bots/${bot}/articles`);

    await page.getByLabel("Search").fill("terms");
    await expect(page).toHaveURL(/[?&]text=terms/);
    await expect(page.getByRole("row").filter({ hasText: "priceList" })).toHaveCount(0);
    await page.reload();
    await expect(page.getByLabel("Search")).toHaveValue("terms");
    await expect(page.getByRole("row").filter({ hasText: "priceList" })).toHaveCount(0);

    await page.getByRole("row").filter({ hasText: "terms" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Back to articles" }).click();
    await expect(page.getByLabel("Search")).toHaveValue("terms");

    const steps = { hello: { say: "Hi" } };
    const procedure = { description: "A procedure", firstStep: "hello", steps, whenStalled: null };
    expect((await request.put(`/api/bots/${bot}/procedures/refund?ifRevision=0`, { data: procedure })).status()).toBe(200);
    await page.goto(`/bots/${bot}/procedures/refund/edit?step=hello`);
    await expect(page.locator('section[aria-label="Step hello"]')).toBeVisible();
    await page.reload();
    await expect(page.locator('section[aria-label="Step hello"]')).toBeVisible();
});

test("AC-delete-guard-20: no row offers Delete, Delete asks first, and declining deletes nothing", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await expect(page.getByRole("row").filter({ hasText: "offer" }).getByRole("button", { name: "Delete" })).toHaveCount(0);

    await page.goto(`/bots/${bot}/knowledge-bases/offer/edit`);
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    const question = page.getByRole("alertdialog");
    await expect(question).toContainText("Delete knowledge base offer?");
    await question.getByRole("button", { name: "Keep it" }).click();

    await expect(question).toHaveCount(0);
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/offer`)).status()).toBe(200);
});

test("AC-delete-guard-21: a procedure is deleted from its edit page, and conversation offers no Delete", async ({ page, request }) => {
    const bot = await newBot(request);
    await addProcedure(request, bot, "refund");
    await addProcedure(request, bot, "conversation");

    await page.goto(`/bots/${bot}/procedures/conversation/edit`);
    await expect(page.getByRole("heading", { name: "Procedure conversation" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Delete", exact: true })).toHaveCount(0);

    await page.goto(`/bots/${bot}/procedures/refund/edit`);
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await page
        .getByRole("alertdialog")
        .getByRole("button", { name: /^Delete/ })
        .click();

    await expect(page).toHaveURL(`/bots/${bot}/procedures`);
    await expect(page.getByRole("row").filter({ hasText: "refund" })).toHaveCount(0);
    expect((await request.get(`/api/bots/${bot}/procedures/refund`)).status()).toBe(404);
});

test("AC-resource-rename-29: the heading shows the id beside its button, with help, and a fact says name", async ({ page, request }) => {
    const bot = await newBot(request);
    await addFact(request, bot, "customer.email");
    await addBase(request, bot, "offer");

    await page.goto(`/bots/${bot}/facts/customer.email/edit`);
    await expect(page.getByRole("heading", { name: "Fact customer.email" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Change id" })).toHaveCount(0);
    await page.getByRole("button", { name: "About changing the name" }).focus();
    await expect(page.getByRole("tooltip")).toContainText("Gives this fact a new name");

    await page.goto(`/bots/${bot}/knowledge-bases/offer/edit`);
    await expect(page.getByRole("heading", { name: "Knowledge base offer" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Change id" })).toBeVisible();
});

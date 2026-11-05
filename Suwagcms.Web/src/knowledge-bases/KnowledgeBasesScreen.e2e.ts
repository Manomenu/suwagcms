// A person works on the knowledge bases of one bot in the browser; the bot is in the address, and a refusal keeps what was typed.
import { expect, test, type APIRequestContext } from "@playwright/test";

function freshId(): string {
    return `kb-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

async function newBot(request: APIRequestContext): Promise<string> {
    const id = freshId();
    const response = await request.post("/api/bots", { data: { id, name: `Name of ${id}`, address: `http://${id}.internal:8080` } });
    expect(response.status()).toBe(201);
    return id;
}

async function addBase(request: APIRequestContext, bot: string, id: string, title: string): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/knowledge-bases/${id}?ifRevision=0`, { data: { title } });
    expect(response.status()).toBe(200);
}

test("AC-knowledge-bases-7: a bot with no bases offers to create the first one, and AC-knowledge-bases-1: the base created is listed and in the API", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await expect(page.getByText("No knowledge bases yet.")).toBeVisible();

    await page.getByRole("button", { name: "New knowledge base" }).click();
    await page.getByLabel("Id").fill("oferta");
    await page.getByLabel("Title").fill("Offer");
    await page.getByRole("button", { name: "Create", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "oferta" });
    await expect(row).toContainText("Offer");
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases`)).json()).toMatchObject([{ id: "oferta", title: "Offer" }]);
});

test("AC-knowledge-bases-2: a base edited in the web keeps its id", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title").fill("Our offer");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("row").filter({ hasText: "oferta" })).toContainText("Our offer");
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases`)).json()).toMatchObject([{ id: "oferta", title: "Our offer" }]);
});

test("AC-knowledge-bases-4: a title another base has is refused beside the title and keeps what was typed", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Our offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("button", { name: "New knowledge base" }).click();
    await page.getByLabel("Id").fill("second");
    await page.getByLabel("Title").fill("  our OFFER ");
    await page.getByRole("button", { name: "Create", exact: true }).click();

    await expect(page.locator('[data-field="title"]')).toContainText("used by knowledge base 'oferta'");
    await expect(page.getByLabel("Id")).toHaveValue("second");
    await expect(page.getByLabel("Title")).toHaveValue("  our OFFER ");
});

test("AC-knowledge-bases-6: editing a base that changed meanwhile is refused with the current revision and keeps what was typed", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "First");
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title").fill("Marta's title");
    const other = await request.put(`/api/bots/${bot}/knowledge-bases/oferta?ifRevision=1`, { data: { title: "The agent's title" } });
    expect(other.status()).toBe(200);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel("Title")).toHaveValue("Marta's title");
});

test("AC-knowledge-bases-3: a base deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("No knowledge bases yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).status()).toBe(404);
});

test("AC-knowledge-bases-5: the screen of one bot shows nothing of another bot's bases", async ({ page, request }) => {
    const first = await newBot(request);
    const second = await newBot(request);
    await addBase(request, first, "oferta", "Offer");
    await addBase(request, second, "oferta", "Offer");
    await addBase(request, first, "onlyHere", "Only here");

    await page.goto(`/bots/${second}/knowledge-bases`);

    await expect(page.getByText("oferta", { exact: true })).toBeVisible();
    await expect(page.getByText("Only here")).toHaveCount(0);
});

test("AC-knowledge-bases-9: a base with articles is not deleted, and the screen lists the articles", async ({ page, request }) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Offer");
    const article = await request.put(`/api/bots/${bot}/articles/priceList?ifRevision=0`, {
        data: { knowledgeBase: "oferta", title: "Price list", content: "c", llmGuide: "g", description: "d" },
    });
    expect(article.status()).toBe(200);
    await page.goto(`/bots/${bot}/knowledge-bases`);

    await page.getByRole("row").filter({ hasText: "oferta" }).getByRole("button", { name: "Delete" }).click();

    await expect(page.getByRole("alert")).toContainText("'priceList' is in the knowledge base 'oferta'");
    await expect(page.getByRole("row").filter({ hasText: "oferta" })).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).status()).toBe(200);
});

test("AC-knowledge-base-language-2: a new base starts on en, the language chosen is saved and shown in the list and the form", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("button", { name: "New knowledge base" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("en");
    await expect(page.locator('[data-field="language"] select').locator("option")).toHaveText(["pl", "en"]);
    await page.getByLabel("Id").fill("oferta");
    await page.getByLabel("Title").fill("Oferta");

    await page.locator('[data-field="language"] select').selectOption("pl");
    await page.getByRole("button", { name: "Create", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "oferta" });
    await expect(row.getByRole("cell", { name: "pl", exact: true })).toBeVisible();
    expect(await (await request.get(`/api/bots/${bot}/knowledge-bases/oferta`)).json()).toMatchObject({ language: "pl" });
    await row.getByRole("button", { name: "Edit" }).click();
    await expect(page.locator('[data-field="language"] select')).toHaveValue("pl");
});

test("AC-knowledge-base-language-11: the label of the language select carries a tooltip saying what the field is for", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/knowledge-bases`);
    await page.getByRole("button", { name: "New knowledge base" }).click();

    await expect(page.locator('[data-field="language"] label')).toHaveAttribute("title", /language of every article in this base/);
});

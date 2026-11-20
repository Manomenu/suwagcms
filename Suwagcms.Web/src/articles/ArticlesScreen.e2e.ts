import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot: newEmptyBot } = testBots("art");

async function newBot(request: APIRequestContext): Promise<string> {
    const id = await newEmptyBot(request);
    for (const [base, title] of [
        ["offer", "Offer"],
        ["forValuers", "For valuers"],
    ]) {
        const created = await request.put(`/api/bots/${id}/knowledge-bases/${base}?ifRevision=0`, {
            data: { title, embedding: "default", results: 5 },
        });
        expect(created.status()).toBe(200);
    }
    return id;
}

async function addBase(request: APIRequestContext, bot: string, id: string, title: string, language: string): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/knowledge-bases/${id}?ifRevision=0`, {
        data: { title, language, embedding: "default", results: 5 },
    });
    expect(response.status()).toBe(200);
}

async function addArticle(
    request: APIRequestContext,
    bot: string,
    id: string,
    extra: { knowledgeBase?: string; title?: string; description?: string; content?: string; llmGuide?: string } = {},
): Promise<void> {
    const response = await request.put(`/api/bots/${bot}/articles/${id}?ifRevision=0`, {
        data: {
            knowledgeBase: extra.knowledgeBase ?? "offer",
            title: extra.title ?? "Price list",
            content: extra.content ?? "What a valuation costs.",
            llmGuide: extra.llmGuide ?? "Ask which kind of property first.",
            description: extra.description ?? "What a valuation costs",
        },
    });
    expect(response.status()).toBe(200);
}

async function fill(page: Page, id: string, title: string): Promise<void> {
    await page.getByLabel("Id", { exact: true }).fill(id);
    await page.getByLabel("Title", { exact: true }).fill(title);
    await page.getByLabel(/^content/).fill("What a valuation costs.");
    await page.getByLabel(/^llmGuide/).fill("Ask which kind of property first.");
    await page.getByLabel(/^description/).fill("What a valuation costs and what affects the price");
}

test("AC-articles-1: an article created in the web is listed in its base and is in the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();
    for (const label of [/^content/, /^llmGuide/, /^description/]) {
        await expect(page.getByLabel(label)).toBeVisible();
    }

    await fill(page, "priceList", "Price list");
    await page.locator('[data-field="knowledgeBase"] select').selectOption("forValuers");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    const row = page.getByRole("row").filter({ hasText: "priceList" });
    await expect(row).toContainText("Price list");
    await expect(row.getByRole("cell", { name: "priceList", exact: true })).toBeVisible();
    await expect(row.getByRole("cell", { name: "forValuers", exact: true })).toBeVisible();
    const listed = await request.get(`/api/bots/${bot}/articles?knowledgeBase=forValuers`);
    expect(await listed.json()).toMatchObject([{ id: "priceList", content: "What a valuation costs." }]);
});

const STARTING_HEIGHT = 192;
const TEXTS = ["#article-content", "#article-llmGuide", "#article-description"];

function longText(lines: number, what: string): string {
    return Array.from({ length: lines }, (_, line) => `Line ${String(line + 1)} of the ${what}.`).join("\n\n");
}

async function boxes(page: Page) {
    return page.evaluate((selectors) => {
        const form = document.querySelector("form");
        if (form === null) throw new Error("no form");
        const id = document.querySelector("#article-id");
        return {
            formWidth: form.getBoundingClientRect().width,
            idTop: id?.getBoundingClientRect().top ?? Number.NaN,
            texts: selectors.map((selector) => {
                const box = document.querySelector(selector);
                if (box === null) throw new Error(`no ${selector}`);
                const rect = box.getBoundingClientRect();
                return {
                    selector,
                    top: rect.top,
                    width: rect.width,
                    height: rect.height,
                    scrollHeight: box.scrollHeight,
                    clientHeight: box.clientHeight,
                };
            }),
        };
    }, TEXTS);
}

test("AC-articles-10: a long article's texts come first, as wide as the form, grown to their whole text with no scroll inside", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList", {
        content: longText(60, "price list"),
        llmGuide: longText(30, "guide"),
        description: longText(30, "description"),
    });

    await openEditor(page, bot, "priceList");

    const { formWidth, idTop, texts } = await boxes(page);
    const tops = [...texts.map((box) => box.top), idTop];
    expect(tops, "content, llmGuide and description come first, in that order").toEqual([...tops].sort((a, b) => a - b));
    expect(new Set(tops).size).toBe(tops.length);
    for (const box of texts) {
        expect(box.width, `${box.selector} is as wide as the form`).toBeCloseTo(formWidth, 0);
        expect(box.scrollHeight, `${box.selector} has no scroll inside`).toBe(box.clientHeight);
        expect(box.height, `${box.selector} grew past its starting height`).toBeGreaterThan(STARTING_HEIGHT);
    }
});

test("AC-articles-10: a short article's texts keep their starting height and grow when long text is pasted", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList");

    await openEditor(page, bot, "priceList");
    for (const box of (await boxes(page)).texts) {
        expect(box.height, `${box.selector} keeps its starting height`).toBe(STARTING_HEIGHT);
    }

    for (const selector of TEXTS) {
        await page.locator(selector).fill(longText(40, selector));
    }
    for (const box of (await boxes(page)).texts) {
        expect(box.scrollHeight, `${box.selector} has no scroll inside`).toBe(box.clientHeight);
        expect(box.height, `${box.selector} grew with the pasted text`).toBeGreaterThan(STARTING_HEIGHT);
    }
});

test("AC-articles-2: a bad article shows every mistake beside its field and keeps what was typed", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();
    await page.getByLabel("Id", { exact: true }).fill("cennik");
    await page.getByLabel("Title", { exact: true }).fill("Price list");

    await page.getByRole("button", { name: "Add", exact: true }).click();

    for (const field of ["content", "llmGuide", "description"]) {
        await expect(page.locator(`[data-field="${field}"]`)).toContainText(`'cennik' has no ${field}.`);
    }
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Price list");
    expect((await (await request.get(`/api/bots/${bot}/articles`)).json()) as unknown[]).toEqual([]);
});

test("AC-articles-3: an id another article has, even in another base, is refused beside the id", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList", { knowledgeBase: "forValuers" });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();

    await fill(page, "priceList", "Another");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.locator('[data-field="id"]')).toContainText("already exists");
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Another");
});

test("AC-articles-6: an edit in the web changes the article and a new base moves it", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList");
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("row").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title", { exact: true }).fill("New title");
    await page.locator('[data-field="knowledgeBase"] select').selectOption("forValuers");
    await page.getByRole("button", { name: "Save" }).click();

    const row = page.getByRole("row").filter({ hasText: "priceList" });
    await expect(row).toContainText("New title");
    await expect(row.getByRole("cell", { name: "priceList", exact: true })).toBeVisible();
    await expect(row.getByRole("cell", { name: "forValuers", exact: true })).toBeVisible();
});

test("AC-articles-7: saving an edit of an article that changed meanwhile is refused with the current revision and keeps what was typed", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList", { description: "First" });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel(/^description/).fill("Marta's text");
    const other = await request.put(`/api/bots/${bot}/articles/priceList?ifRevision=1`, {
        data: {
            knowledgeBase: "offer",
            title: "Price list",
            content: "c",
            llmGuide: "g",
            description: "The agent's text",
        },
    });
    expect(other.status()).toBe(200);

    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("alert")).toContainText("it is now revision 2");
    await expect(page.getByLabel(/^description/)).toHaveValue("Marta's text");
    await page.getByRole("button", { name: "Reload the article" }).click();
    await expect(page.getByLabel(/^description/)).toHaveValue("Marta's text");
    await expect(async () => {
        await page.getByRole("button", { name: "Save" }).click();
        await expect(page.getByRole("row").filter({ hasText: "priceList" })).toContainText("Marta's text", { timeout: 1000 });
    }).toPass();
});

test("AC-articles-8: an article deleted in the web is gone from the list and from the API", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "priceList");
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("row").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await page
        .getByRole("alertdialog")
        .getByRole("button", { name: /^Delete/ })
        .click();

    await expect(page.getByText("No articles yet.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/articles/priceList`)).status()).toBe(404);
});

test("AC-articles-11: the list filters by base and by text, the same as the API does", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "discounts", { title: "Prices", description: "Is there a RABAT" });
    await addArticle(request, bot, "other", { title: "Other", description: "Unrelated", content: "A rabat is a discount." });
    await addArticle(request, bot, "elsewhere", { knowledgeBase: "forValuers", title: "Rabat", description: "Rabat" });
    await page.goto(`/bots/${bot}/articles`);
    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(3);

    await page.getByLabel("Search").fill("rabat");
    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(2);
    await page.getByLabel("Knowledge base", { exact: true }).selectOption("offer");

    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(1);
    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toContainText("discounts");
    const api = await request.get(`/api/bots/${bot}/articles?knowledgeBase=offer&text=rabat`);
    expect(((await api.json()) as { id: string }[]).map((article) => article.id)).toEqual(["discounts"]);
});

test("AC-articles-13: leaving the editor with unsaved changes asks before discarding them", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();
    await page.getByLabel("Title", { exact: true }).fill("Unsaved");
    const questions: string[] = [];
    let accept = false;
    page.on("dialog", (dialog) => {
        questions.push(dialog.message());
        if (accept) void dialog.accept();
        else void dialog.dismiss();
    });

    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Unsaved");
    await page.getByRole("link", { name: "Bots" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/articles/new`);
    accept = true;
    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Title", { exact: true })).toHaveCount(0);
    expect(questions).toEqual(Array(3).fill("Discard the changes you have not saved?"));
});

test("AC-articles-13: closing an untouched editor asks nothing", async ({ page, request }) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();
    page.on("dialog", () => {
        throw new Error("no question was expected");
    });

    await page.getByRole("button", { name: "Cancel" }).click();

    await expect(page.getByLabel("Title", { exact: true })).toHaveCount(0);
});

test("AC-articles-15: the articles screen of one bot shows nothing of another bot's articles", async ({ page, request }) => {
    const first = await newBot(request);
    const second = await newBot(request);
    await addArticle(request, first, "priceList", { title: "Of the first" });

    await page.goto(`/bots/${second}/articles`);

    await expect(page.getByText("No articles yet.")).toBeVisible();
    await expect(page.getByText("Of the first")).toHaveCount(0);
});

test("AC-knowledge-base-language-12: the article form shows the language of the chosen base as read-only text that follows the selector, with a tooltip", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Oferta", "pl");
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();

    await expect(page.locator('[data-field="language"] select')).toHaveCount(0);
    await expect(page.locator('[data-field="language"] output')).toHaveText("en");
    await page.locator('[data-field="knowledgeBase"] select').selectOption("oferta");
    await expect(page.locator('[data-field="language"] output')).toHaveText("pl");
    await expect(page.locator('[data-field="language"] button')).toContainText(/language of the knowledge base the article is in/);
});

test("AC-knowledge-base-language-9: the list filters by language together with the other filters, the same as the API does", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot, "oferta", "Oferta", "pl");
    await addBase(request, bot, "cenniki", "Cenniki", "pl");
    await addArticle(request, bot, "cennik", { knowledgeBase: "oferta", title: "Cennik", description: "Ile kosztuje wycena" });
    await addArticle(request, bot, "pricing", { title: "Pricing", description: "What a valuation costs" });
    await addArticle(request, bot, "cennikBiegly", { knowledgeBase: "cenniki", title: "Cennik", description: "Ile kosztuje" });
    await page.goto(`/bots/${bot}/articles`);
    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(3);

    await page.getByLabel("Language", { exact: true }).selectOption("pl");
    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(2);
    await page.getByLabel("Knowledge base", { exact: true }).selectOption("oferta");

    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(1);
    const row = page.getByRole("main").locator("tbody").getByRole("row");
    await expect(row.getByRole("cell", { name: "cennik", exact: true })).toBeVisible();
    await expect(row.getByRole("cell", { name: "oferta", exact: true })).toBeVisible();
    await expect(row.getByRole("cell", { name: "pl", exact: true })).toBeVisible();
    const api = await request.get(`/api/bots/${bot}/articles?language=pl&knowledgeBase=oferta`);
    expect(((await api.json()) as { id: string }[]).map((article) => article.id)).toEqual(["cennik"]);
});

test("AC-articles-4: editing another field of an article whose text has CRLF line breaks leaves the stored text byte for byte", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const content = "# Cennik\r\n\r\n- zażółć gęślą jaźń\r\n\n\n";
    await addArticle(request, bot, "priceList", { content });
    await page.goto(`/bots/${bot}/articles`);

    await page.getByRole("row").filter({ hasText: "priceList" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel("Title", { exact: true }).fill("Edited title");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("row").filter({ hasText: "priceList" })).toContainText("Edited title");
    expect(await (await request.get(`/api/bots/${bot}/articles/priceList`)).json()).toMatchObject({ title: "Edited title", content });
});

async function openEditor(page: Page, bot: string, id: string): Promise<void> {
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: id }).getByRole("button", { name: "Edit" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toBeVisible();
}

async function contentOf(request: APIRequestContext, bot: string, id: string): Promise<string> {
    return ((await (await request.get(`/api/bots/${bot}/articles/${id}`)).json()) as { content: string }).content;
}

test("AC-article-links-1: choosing an article in the picker puts a chip with its title in the text and stores the Markdown link there", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { title: "Valuation", content: "See the " });
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await page.keyboard.type("cen");
    await page
        .getByRole("listbox", { name: "What to link" })
        .getByRole("option", { name: /Price list/ })
        .click();
    await page.keyboard.type("now.");

    await expect(page.locator('[data-markdown-editor="content"] [data-article-link="cennik"]')).toHaveText("Price list");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toHaveCount(0);
    expect(await contentOf(request, bot, "wycena")).toBe("See the [Price list](cms://article/cennik) now.");
});

test("AC-article-links-2: the picker lists the articles of every base of this bot, narrows by title or id, leaves out the article being edited and shows none of another bot", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    const other = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list", knowledgeBase: "offer" });
    await addArticle(request, bot, "rules", { title: "Rules of valuers", knowledgeBase: "forValuers" });
    await addArticle(request, bot, "wycena", { title: "Valuation" });
    await addArticle(request, other, "secret", { title: "Secret price" });
    await openEditor(page, bot, "wycena");
    const options = page.getByRole("listbox", { name: "What to link" }).getByRole("option");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await expect(options).toHaveText([/Price list/, /Rules of valuers/]);
    await page.keyboard.type("rul");
    await expect(options).toHaveText([/Rules of valuers/]);
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.type("cenn");
    await expect(options).toHaveText([/Price list/]);
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.press("Backspace");
    await page.keyboard.type("secret");
    await expect(page.getByText("Nothing matches")).toBeVisible();
});

test("AC-article-links-3: opening an article with links and saving another field leaves its text as stored", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    const content =
        "# Heading\n\n* star list\n* with [Prices](cms://article/cennik)\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\nSome _emphasis_.\r\n";
    await addArticle(request, bot, "wycena", { content });
    await openEditor(page, bot, "wycena");

    await page.getByLabel("Title", { exact: true }).fill("Edited");
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("row").filter({ hasText: "wycena" })).toContainText("Edited");
    expect(await contentOf(request, bot, "wycena")).toBe(content);
});

test("AC-article-links-4: a chip shows the current title of its target, while the stored label stays", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { content: "See [Price list](cms://article/cennik)." });
    await request.put(`/api/bots/${bot}/articles/cennik?ifRevision=1`, {
        data: { knowledgeBase: "offer", title: "Prices", content: "c", llmGuide: "g", description: "d" },
    });
    await openEditor(page, bot, "wycena");

    await expect(page.locator('[data-article-link="cennik"]')).toHaveText("Prices");
    await page.getByLabel("Title", { exact: true }).fill("Valuation");
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("row").filter({ hasText: "wycena" })).toContainText("Valuation");
    expect(await contentOf(request, bot, "wycena")).toBe("See [Price list](cms://article/cennik).");
});

test("AC-article-links-5: clicking a chip opens its article, and deleting a chip removes the link from the saved text", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    await addArticle(request, bot, "wycena", { content: "See [Price list](cms://article/cennik)" });
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.evaluate(() => new Promise(requestAnimationFrame));
    await page.keyboard.press("Backspace");
    await expect(page.locator("[data-article-link]")).toHaveCount(0);
    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("textbox", { name: "content" })).toHaveCount(0);
    expect(await contentOf(request, bot, "wycena")).toBe("See ");

    await openEditor(page, bot, "cennik");
    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("cennik");
    await request.put(`/api/bots/${bot}/articles/wycena?ifRevision=2`, {
        data: {
            knowledgeBase: "offer",
            title: "Valuation",
            content: "See [Price list](cms://article/cennik)",
            llmGuide: "g",
            description: "d",
        },
    });
    await openEditor(page, bot, "wycena");
    await page.locator('[data-article-link="cennik"]').click();
    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("cennik");
    await expect(page.getByLabel("Title", { exact: true })).toHaveValue("Price list");
});

test("AC-article-links-6: a link saved as Markdown through the API, as the CLI does, is a chip in the web, in content and in llmGuide", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { title: "Price list" });
    const saved = await request.put(`/api/bots/${bot}/articles/wycena?ifRevision=0`, {
        data: {
            knowledgeBase: "offer",
            title: "Valuation",
            content: "See [Price list](cms://article/cennik).",
            llmGuide: "Mention [it](cms://article/cennik).",
            description: "d",
        },
    });
    expect(saved.status()).toBe(200);
    await openEditor(page, bot, "wycena");

    await expect(page.locator('[data-markdown-editor="content"] [data-article-link="cennik"]')).toHaveText("Price list");
    await expect(page.locator('[data-markdown-editor="llmGuide"] [data-article-link="cennik"]')).toHaveText("Price list");
});

test("AC-loop-guard-1: a link that closes a loop is refused beside the field, with the path, and nothing is saved", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "pricing", { title: "Pricing", content: "See " });
    await addArticle(request, bot, "discounts", { title: "Discounts", content: "See [Pricing](cms://article/pricing)." });
    await openEditor(page, bot, "pricing");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await page.keyboard.type("disc");
    await page
        .getByRole("listbox", { name: "What to link" })
        .getByRole("option", { name: /Discounts/ })
        .click();
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.locator('[data-field="content"]')).toContainText("discounts → pricing → discounts");
    expect(await contentOf(request, bot, "pricing")).toBe("See ");
});

test("AC-delete-guard-9: an article another article links to is refused for delete in the API and its Delete button is disabled in the web", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    await addArticle(request, bot, "wycena", { content: "See [it](cms://article/cennik)." });

    const refused = await request.delete(`/api/bots/${bot}/articles/cennik`);
    expect(refused.status()).toBe(422);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("button", { name: "Delete" })).toBeDisabled();
    await expect(page.getByText("Used in 1 place. Remove that reference first.")).toBeVisible();
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(200);
});

test("AC-delete-guard-7: the panels list who uses the article and what it uses as working links, and say so when there are none", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "warunki");
    await addArticle(request, bot, "cennik", { content: "See [w](cms://article/warunki)." });
    await addArticle(request, bot, "wycena", { content: "See [c](cms://article/cennik)." });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("region", { name: "Used by" })).toContainText("wycena");
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("content");
    await expect(page.getByRole("region", { name: "Uses" })).toContainText("warunki");
    await page.getByRole("region", { name: "Used by" }).getByRole("link", { name: "wycena" }).click();

    await expect(page.getByLabel("Id", { exact: true })).toHaveValue("wycena");
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("Nothing uses this.");
});

test("AC-delete-guard-4: after the last link is removed in the web the article can be deleted", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    await addArticle(request, bot, "wycena", { content: "See [it](cms://article/cennik)." });
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "wycena" }).getByRole("button", { name: "Edit" }).click();
    await page.getByLabel(/^content/).fill("No link now.");
    await page.getByRole("button", { name: "Save" }).click();
    await page.getByRole("row").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("region", { name: "Used by" })).toContainText("Nothing uses this.");
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await page
        .getByRole("alertdialog")
        .getByRole("button", { name: /^Delete/ })
        .click();

    await expect(page.getByRole("main").locator("tbody").getByRole("row")).toHaveCount(1);
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(404);
});

test("AC-knowledge-base-language-12: the language stands beside the base selector, and the id, title and base share their top and height", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("button", { name: "Add an article" }).click();
    await expect(page.getByLabel(/^description/)).toBeVisible();

    const boxOf = async (selector: string) => {
        const box = await page.locator(selector).boundingBox();
        if (box === null) throw new Error(`${selector} has no box`);
        return { top: Math.round(box.y), height: Math.round(box.height) };
    };
    const row = [await boxOf("#article-id"), await boxOf("#article-title"), await boxOf("#article-knowledgeBase")];
    expect(row[1]).toEqual(row[0]);
    expect(row[2]).toEqual(row[0]);
    const language = await page.locator('[data-field="language"]').boundingBox();
    const selector = await page.locator('[data-field="knowledgeBase"]').boundingBox();
    if (language === null || selector === null) throw new Error("the language or the base selector has no box");
    expect(language.y).toBeCloseTo(selector.y, 0);
    expect(language.x).toBeGreaterThanOrEqual(selector.x + selector.width);
});

test("AC-resource-rename-2: Change id lists the referrers before it is confirmed, rewrites them, and leaves the author on the article at its new id", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { knowledgeBase: "offer", title: "Price list" });
    await addArticle(request, bot, "wycena", {
        knowledgeBase: "offer",
        title: "Valuation",
        content: "See the [Price list](cms://article/cennik).",
    });
    await openEditor(page, bot, "cennik");

    await page.getByRole("button", { name: "Change id" }).click();
    const dialog = page.getByRole("dialog", { name: "Change id" });
    await dialog.getByLabel("New id").fill("Bad Id");
    await expect(dialog).toContainText("'Bad Id' is not an article id");
    await expect(dialog.getByRole("button", { name: "Change id" })).toBeDisabled();

    await dialog.getByLabel("New id").fill("priceList");
    await expect(dialog).toContainText("This rewrites 1 referrer");
    await expect(dialog).toContainText("wycena content, 1 link");
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(200);
    await dialog.getByRole("button", { name: "Change id" }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("wycena");
    await expect(page.getByLabel("Title")).toHaveValue("Price list");
    expect(await contentOf(request, bot, "wycena")).toBe("See the [Price list](cms://article/priceList).");
    expect((await request.get(`/api/bots/${bot}/articles/cennik`)).status()).toBe(404);
});

test("AC-resource-rename-12: a dialog opened at an old revision is refused with the current one", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik", { knowledgeBase: "offer", title: "Price list" });
    await openEditor(page, bot, "cennik");
    await page.getByRole("button", { name: "Change id" }).click();
    const stored = (await (await request.get(`/api/bots/${bot}/articles/cennik`)).json()) as Record<string, unknown>;
    const { id: _id, revision: _revision, language: _language, ...body } = stored;
    expect((await request.put(`/api/bots/${bot}/articles/cennik`, { data: { ...body, title: "Prices" } })).status()).toBe(200);

    const dialog = page.getByRole("dialog", { name: "Change id" });
    await dialog.getByLabel("New id").fill("priceList");

    await expect(dialog).toContainText("it is now revision 2");
    await expect(dialog.getByRole("button", { name: "Change id" })).toBeDisabled();
});

test("AC-article-procedure-links-1: in the guide the picker offers a procedure, the chip shows its id, the saved text holds the link, and the chip opens the procedure", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "wycena", { title: "Valuation", llmGuide: "If so, start " });
    expect(
        (
            await request.put(`/api/bots/${bot}/procedures/orderValuation?ifRevision=0`, {
                data: { description: "The customer orders a valuation" },
            })
        ).status(),
    ).toBe(200);
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "llmGuide" }).click();
    await page.keyboard.press("Control+End");
    await page.locator('[data-markdown-editor="llmGuide"]').getByRole("button", { name: "Insert link" }).click();
    await page.keyboard.type("orderV");
    await page
        .getByRole("listbox", { name: "What to link" })
        .getByRole("option", { name: /procedure.*orderValuation/ })
        .click();
    const chip = page.locator('[data-markdown-editor="llmGuide"] [data-procedure-link="orderValuation"]');
    await expect(chip).toHaveText("orderValuation");

    await page.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("textbox", { name: "llmGuide" })).toHaveCount(0);
    const stored = (await (await request.get(`/api/bots/${bot}/articles/wycena`)).json()) as { llmGuide: string };
    expect(stored.llmGuide).toContain("[orderValuation](cms://procedure/orderValuation)");

    await openEditor(page, bot, "wycena");
    await page.locator('[data-markdown-editor="llmGuide"] [data-procedure-link="orderValuation"]').click();
    await expect(page).toHaveURL(new RegExp(`/bots/${bot}/procedures/orderValuation/edit$`));
});

test("AC-article-procedure-links-2: the picker in content offers no procedure", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "wycena", { title: "Valuation", content: "See " });
    await request.put(`/api/bots/${bot}/procedures/orderValuation?ifRevision=0`, { data: { description: "D" } });
    await openEditor(page, bot, "wycena");

    await page.getByRole("textbox", { name: "content" }).click();
    await page.keyboard.press("Control+End");
    await page.locator('[data-markdown-editor="content"]').getByRole("button", { name: "Insert link" }).click();
    await page.keyboard.type("orderValuation");

    await expect(page.getByRole("listbox", { name: "What to link" })).toContainText("Nothing matches");
});

test("AC-delete-guard-19: the panels ask again when a reload brings a newer revision of the article", async ({ page, request }) => {
    const bot = await newBot(request);
    await addArticle(request, bot, "cennik");
    await page.goto(`/bots/${bot}/articles`);
    await page.getByRole("row").filter({ hasText: "cennik" }).getByRole("button", { name: "Edit" }).click();
    await expect(page.getByRole("region", { name: "Used by" })).toContainText("Nothing uses this.");

    await addArticle(request, bot, "wycena", { content: "See [it](cms://article/cennik)." });
    const changed = await request.put(`/api/bots/${bot}/articles/cennik?ifRevision=1`, {
        data: { knowledgeBase: "offer", title: "Changed", content: "Changed", llmGuide: "Guide.", description: "Changed" },
    });
    expect(changed.status()).toBe(200);
    await page.getByRole("button", { name: "Save" }).click();
    await page.getByRole("button", { name: "Reload the article" }).click();

    await expect(page.getByRole("region", { name: "Used by" })).toContainText("wycena");
});

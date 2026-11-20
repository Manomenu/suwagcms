import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { testBots } from "../../e2e/bots";

const { newBot } = testBots("pages");

async function addFacts(request: APIRequestContext, bot: string, count: number): Promise<void> {
    for (let number = 10; number < 10 + count; number++) {
        const data = { type: "text", description: `Field ${String(number)}`, maxLength: 80 };
        expect((await request.put(`/api/bots/${bot}/facts/customer.f${String(number)}?ifRevision=0`, { data })).status()).toBe(200);
    }
}

async function addBase(request: APIRequestContext, bot: string): Promise<void> {
    expect(
        (
            await request.put(`/api/bots/${bot}/knowledge-bases/offer?ifRevision=0`, {
                data: { title: "Offer", embedding: "default", results: 5 },
            })
        ).status(),
    ).toBe(200);
}

function listPages(page: Page) {
    return page.locator('[aria-label="Pages"]:not([aria-label="History"] *)');
}

function next(page: Page) {
    return listPages(page).getByRole("button", { name: "Next", exact: true });
}

function previous(page: Page) {
    return listPages(page).getByRole("button", { name: "Previous", exact: true });
}

test("AC-pagination-5: Next writes the page to the address; a reload and a sent link show it, Back returns, and page 1 has no page", async ({
    page,
    request,
    context,
}) => {
    const bot = await newBot(request);
    await addFacts(request, bot, 12);
    await page.goto(`/bots/${bot}/facts`);
    await expect(listPages(page).getByText("Page 1 of 3")).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/facts`);

    await next(page).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts?page=2`);
    await expect(listPages(page).getByText("Page 2 of 3")).toBeVisible();
    await expect(page.getByRole("row").filter({ hasText: "customer.f15" })).toBeVisible();
    await expect(page.getByRole("row").filter({ hasText: "customer.f14" })).toHaveCount(0);

    await page.reload();
    await expect(listPages(page).getByText("Page 2 of 3")).toBeVisible();
    const sent = await context.newPage();
    await sent.goto(`/bots/${bot}/facts?page=2`);
    await expect(sent.getByRole("row").filter({ hasText: "customer.f15" })).toBeVisible();
    await sent.close();

    await page.goBack();
    await expect(page).toHaveURL(`/bots/${bot}/facts`);
    await expect(listPages(page).getByText("Page 1 of 3")).toBeVisible();

    await page.goto(`/bots/${bot}/facts?page=2`);
    await previous(page).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts`);

    await page.goto(`/bots/${bot}/facts?page=99`);
    await expect(listPages(page).getByText("Page 3 of 3")).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/facts?page=3`);
    await page.goto(`/bots/${bot}/facts?page=abc`);
    await expect(listPages(page).getByText("Page 1 of 3")).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/facts`);
});

test("AC-pagination-5: moving brings the top of the list back into view", async ({ page, request }) => {
    const bot = await newBot(request);
    await addFacts(request, bot, 8);
    await page.setViewportSize({ width: 1440, height: 260 });
    await page.goto(`/bots/${bot}/facts`);

    await next(page).scrollIntoViewIfNeeded();
    await expect(page.getByRole("columnheader", { name: "Name" })).not.toBeInViewport();
    await next(page).click();

    await expect(listPages(page).getByText("Page 2 of 2")).toBeAttached();
    await expect(page.getByRole("columnheader", { name: "Name" })).toBeInViewport();
});

test("AC-pagination-6: opening a row on page 2 and saving, cancelling or going back returns to page 2", async ({ page, request }) => {
    const bot = await newBot(request);
    await addFacts(request, bot, 7);
    await page.goto(`/bots/${bot}/facts?page=2`);
    const open = () => page.getByRole("row").filter({ hasText: "customer.f15" }).getByRole("button", { name: "Edit" }).click();

    await open();
    await expect(page).toHaveURL(`/bots/${bot}/facts/customer.f15/edit?page=2`);
    await page.getByRole("button", { name: "Back to facts" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts?page=2`);

    await open();
    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts?page=2`);

    await open();
    await page.getByLabel("Description").fill("A changed field");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page).toHaveURL(`/bots/${bot}/facts?page=2`);
    await expect(listPages(page).getByText("Page 2 of 2")).toBeVisible();
});

test("AC-pagination-6: the creator of a procedure on page 2 returns to page 2 too", async ({ page, request }) => {
    const bot = await newBot(request);
    for (const id of ["a1", "a2", "a3", "a4", "a5", "a6"]) {
        const data = { description: "A procedure", firstStep: null, steps: {}, whenStalled: null };
        expect((await request.put(`/api/bots/${bot}/procedures/${id}?ifRevision=0`, { data })).status()).toBe(200);
    }
    await page.goto(`/bots/${bot}/procedures?page=2`);
    await expect(listPages(page).getByText("Page 2 of 2")).toBeVisible();

    await page.getByRole("row").getByRole("button", { name: "Edit" }).first().click();
    await expect(page).toHaveURL(/\/edit\?page=2$/);
    await page.getByRole("button", { name: "Back to procedures" }).click();

    await expect(page).toHaveURL(`/bots/${bot}/procedures?page=2`);
});

test("AC-pagination-6: deleting the only row of the last page shows the new last page with the address corrected", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addFacts(request, bot, 11);
    await page.goto(`/bots/${bot}/facts?page=3`);
    await expect(listPages(page).getByText("Page 3 of 3")).toBeVisible();

    await page.getByRole("row").filter({ hasText: "customer.f20" }).getByRole("button", { name: "Edit" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await page
        .getByRole("alertdialog")
        .getByRole("button", { name: /^Delete/ })
        .click();

    await expect(page).toHaveURL(`/bots/${bot}/facts?page=2`);
    await expect(listPages(page).getByText("Page 2 of 2")).toBeVisible();
    await expect(page.getByRole("row").filter({ hasText: "customer.f19" })).toBeVisible();
});

test("AC-pagination-7: changing an article filter on page 2 shows page 1 of the result, with the other filters still in the address", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addBase(request, bot);
    for (let number = 10; number < 22; number++) {
        const data = { knowledgeBase: "offer", title: `Title ${String(number)}`, content: "c", llmGuide: "g", description: "d" };
        expect((await request.put(`/api/bots/${bot}/articles/a${String(number)}?ifRevision=0`, { data })).status()).toBe(200);
    }
    await page.goto(`/bots/${bot}/articles?text=Title&page=2`);
    await expect(listPages(page).getByText("Page 2 of 3")).toBeVisible();

    await page.getByLabel("Knowledge base").selectOption("offer");

    await expect(page).toHaveURL(/[?&]text=Title/);
    await expect(page).toHaveURL(/[?&]knowledgeBase=offer/);
    await expect(page).not.toHaveURL(/[?&]page=/);
    await expect(listPages(page).getByText("Page 1 of 3")).toBeVisible();
});

test("AC-pagination-8: rows that fit on one page have controls neither seen nor reached, whose space is as tall as on a list of two pages", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await addFacts(request, bot, 5);
    await page.goto(`/bots/${bot}/facts`);
    await expect(page.getByRole("row").filter({ hasText: "customer.f14" })).toBeVisible();

    const controls = listPages(page);
    await expect(controls).toBeHidden();
    const buttons = controls.locator("button");
    await expect(buttons).toHaveCount(2);
    for (const button of await buttons.all()) {
        await expect.soft(button).toBeHidden();
        const focused = await button.evaluate((element) => {
            (element as HTMLElement).focus();
            return document.activeElement === element;
        });
        expect.soft(focused).toBe(false);
    }
    const alone = await controls.boundingBox();

    const longer = await newBot(request);
    await addFacts(request, longer, 6);
    await page.goto(`/bots/${longer}/facts`);
    await expect(next(page)).toBeVisible();
    const shared = await controls.boundingBox();

    expect(alone?.height).toBeGreaterThan(0);
    expect(alone?.height).toBe(shared?.height);
});

test("AC-pagination-10: on Versions, creating a version on a later page shows page 1", async ({ page, request }) => {
    const bot = await newBot(request);
    const steps = { wait: { listen: {}, next: "answer" }, answer: { say: "Hello", next: "wait" } };
    const conversation = { description: "The base", firstStep: "wait", steps };
    expect((await request.put(`/api/bots/${bot}/procedures/conversation?ifRevision=0`, { data: conversation })).status()).toBe(200);
    expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    for (let number = 1; number <= 6; number++) {
        const data = { type: "text", description: "A field", maxLength: 80 };
        expect((await request.put(`/api/bots/${bot}/facts/customer.f${String(number)}?ifRevision=0`, { data })).status()).toBe(200);
        expect((await request.post(`/api/bots/${bot}/versions`)).status()).toBe(201);
    }
    await page.goto(`/bots/${bot}/versions`);
    await expect(listPages(page).getByText("Page 1 of 2")).toBeVisible();

    await next(page).click();
    await expect(page).toHaveURL(`/bots/${bot}/versions?page=2`);
    await expect(page.getByRole("row").filter({ hasText: "v1 ·" })).toBeVisible();

    const data = { type: "text", description: "One more", maxLength: 80 };
    expect((await request.put(`/api/bots/${bot}/facts/customer.more?ifRevision=0`, { data })).status()).toBe(200);
    await page.getByRole("button", { name: "Create version" }).click();
    await expect(page.getByRole("row").filter({ hasText: "v8 ·" })).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/versions`);
});

async function twelveEntries(request: APIRequestContext, bot: string): Promise<void> {
    await addBase(request, bot);
    for (let revision = 1; revision <= 11; revision++) {
        const data = { title: `Offer ${String(revision)}`, embedding: "default", results: 5 };
        expect((await request.put(`/api/bots/${bot}/knowledge-bases/offer?ifRevision=${String(revision)}`, { data })).status()).toBe(200);
    }
}

for (const [name, address] of [
    ["of a bot", (bot: string) => `/bots/${bot}/history`],
    ["of a resource", (bot: string) => `/bots/${bot}/history?kind=knowledge-bases&id=offer`],
] as const) {
    test(`AC-pagination-11: the History page ${name} shows the 5 newest, Next the next 5, a reload stays, and there is no load-more`, async ({
        page,
        request,
    }) => {
        const bot = await newBot(request);
        await twelveEntries(request, bot);
        await page.goto(address(bot));
        const revisions = page.getByText(/^revision \d+$/);

        await expect(listPages(page).getByText("Page 1 of 3")).toBeVisible();
        await expect(revisions).toHaveText(["revision 12", "revision 11", "revision 10", "revision 9", "revision 8"]);
        await expect(page.getByRole("button", { name: "Older changes" })).toHaveCount(0);

        await next(page).click();
        await expect(page).toHaveURL(/[?&]page=2$/);
        await expect(revisions).toHaveText(["revision 7", "revision 6", "revision 5", "revision 4", "revision 3"]);

        await page.reload();
        await expect(listPages(page).getByText("Page 2 of 3")).toBeVisible();
        await expect(revisions).toHaveText(["revision 7", "revision 6", "revision 5", "revision 4", "revision 3"]);

        await page.goto(`${address(bot)}${address(bot).includes("?") ? "&" : "?"}page=99`);
        await expect(listPages(page).getByText("Page 3 of 3")).toBeVisible();
        await expect(revisions).toHaveText(["revision 2", "revision 1"]);
        await expect(page).toHaveURL(/[?&]page=3$/);
    });
}

test("AC-pagination-12: the History panel pages under historyPage: the list's page stays, typed text stays with no question, and leaving drops it", async ({
    page,
    request,
}) => {
    const bot = await newBot(request);
    await twelveEntries(request, bot);
    for (const id of ["a1", "a2", "a3", "a4", "a5"]) {
        expect(
            (
                await request.put(`/api/bots/${bot}/knowledge-bases/${id}?ifRevision=0`, {
                    data: { title: id, embedding: "default", results: 5 },
                })
            ).status(),
        ).toBe(200);
    }
    await page.goto(`/bots/${bot}/knowledge-bases/offer/edit?page=2`);
    let asked = false;
    page.on("dialog", (dialog) => {
        asked = true;
        void dialog.accept();
    });
    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.getByText("Page 1 of 3")).toBeVisible();

    await page.getByLabel("Title").fill("Typed but not saved");
    await panel.getByRole("button", { name: "Next", exact: true }).click();

    await expect(panel.getByText("Page 2 of 3")).toBeVisible();
    await expect(panel.getByText("revision 7")).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/knowledge-bases/offer/edit?page=2&historyPage=2`);
    await expect(page.getByLabel("Title")).toHaveValue("Typed but not saved");
    expect(asked).toBe(false);

    await page.reload();
    await expect(panel.getByText("Page 2 of 3")).toBeVisible();

    await page.getByRole("button", { name: "Back to knowledge bases" }).click();
    await expect(page).toHaveURL(`/bots/${bot}/knowledge-bases?page=2`);
});

test("AC-pagination-12: a save in Persona's panel on a later page shows the newest page", async ({ page, request }) => {
    const bot = await newBot(request);
    for (let save = 1; save <= 7; save++) {
        expect((await request.put(`/api/bots/${bot}/persona`, { data: { personality: `Calm ${String(save)}` } })).status()).toBe(200);
    }
    await page.goto(`/bots/${bot}/persona?historyPage=2`);
    const panel = page.getByRole("region", { name: "History" });
    await expect(panel.getByText("Page 2 of 2")).toBeVisible();

    await page.getByLabel("Personality").fill("Calm and new");
    await page.getByRole("button", { name: "Save", exact: true }).click();

    await expect(panel.getByText("revision 8")).toBeVisible();
    await expect(page).toHaveURL(`/bots/${bot}/persona`);
});

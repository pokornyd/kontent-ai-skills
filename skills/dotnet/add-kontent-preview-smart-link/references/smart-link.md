# Smart Link: beyond the basic markup

Read when the user asks for add buttons, when a page mixes languages, when the app restricts `script-src`, or
when editors report that overlays do not appear. SDK reference: https://github.com/kontent-ai/smart-link

## The attribute contract

The SDK resolves a click by walking up the DOM, so nesting is the contract:

```html
<body data-kontent-environment-id="…" data-kontent-language-codename="default">
  <article data-kontent-item-id="…">                       <!-- a content item -->
    <h1 data-kontent-element-codename="title">…</h1>         <!-- an element of that item -->
    <div data-kontent-element-codename="body_copy">
      <aside data-kontent-component-id="…">                  <!-- a component inside that rich text -->
        <strong data-kontent-element-codename="headline">…</strong>
      </aside>
    </div>
    <span data-kontent-item-id="…">                          <!-- a linked item: a new item scope -->
      <span data-kontent-element-codename="first_name">…</span>
    </span>
  </article>
</body>
```

An element codename outside any item scope, or a component id outside the rich-text element that contains it,
resolves to nothing. Environment and language can instead be passed once to the SDK,
`initializeOnLoad({ defaultDataAttributes: { environmentId, languageCodename } })`; use one way, not both.

**Component or linked item?** Both reach a rich-text resolver as `IEmbeddedContent<T>`. A component exists only
inside that rich text and is addressed with `data-kontent-component-id`; an item linked into rich text is a
real content item and takes `data-kontent-item-id`. `scripts/verify_preview.py` reports the components it
found in the item, and in the Delivery response components are the `modular_content` entries referenced from
the element with `data-rel="component"`. When a type is used both ways, the resolver cannot tell from the model;
default to the component id, which is the common case, and say so in the report.

**What to mark up.** The fields an editor would want to click: headings, text, images, rich text, dates. Not
every element needs an attribute, and structural elements the view does not render (slugs, SEO metadata) have
nothing to attach to.

## More than one language on a page

`data-kontent-language-codename` can be repeated on any container and the nearest ancestor wins. When a
listing falls back to the default language for some items, put the item's own `System.Language` on its
container, so a click opens the variant that was actually rendered rather than an empty translation.

## Add buttons

`data-kontent-add-button` on the container of a linked-items or rich-text element, inside the item and element
scope, with `data-kontent-add-button-insert-position` (`start`, `end`, or `before`/`after` when placed on an
individual item) and `data-kontent-add-button-render-position` (for example `bottom`, `left-start`). They let
an editor add a linked item or component from the page. Add them only when asked: they are visible chrome in
preview and need the element's allowed types to make sense.

## Refresh after saving

Inside live preview the SDK receives a refresh message when the editor saves and reloads the page, which is the
right behaviour for a server-rendered site. The reload can arrive before the Preview API has the new version;
`WaitForLoadingNewContent(preview.IsPreview)` on the queries closes that gap. Live in-place updates (the SDK's
`Update` event) are for client-rendered frontends holding items in JavaScript state and do not apply here.

## Overlays do not appear

1. In a plain browser tab the SDK stays dormant without `?ksl-enabled` in the URL. Inside live preview it
   activates by itself.
2. View source: is the `<script>` there? If not, the request is not in preview: the cookie was dropped. Over
   plain `http` on a host other than `localhost` a browser discards a `Secure` cookie; inside Kontent.ai a
   browser that blocks third-party cookies discards it too.
3. The page does not load in the preview pane at all: open the browser console. "Refused to display … in a
   frame" is `X-Frame-Options` or a `frame-ancestors` that lacks `https://app.kontent.ai`; mixed content means
   the preview URL is `http`.
4. Overlays appear but open the wrong item or nothing: an id or codename is wrong, or the nesting is. Turn on
   `initializeOnLoad({ debug: true })` and read the console.
5. `data-kontent-project-id` anywhere means markup copied from a v4 example; v5 reads
   `data-kontent-environment-id`.

## A restrictive `script-src`

Allow `https://cdn.jsdelivr.net` for preview responses, or self-host: download the pinned bundle
(`https://cdn.jsdelivr.net/npm/@kontent-ai/smart-link@5/dist/bundles/kontent-smart-link.min.js`) into
`wwwroot/lib/kontent-smart-link/`, point `_SmartLinkScript.cshtml` at it and record the exact version. The
inline `initializeOnLoad()` call needs a nonce or has to move into a static file under the same policy.

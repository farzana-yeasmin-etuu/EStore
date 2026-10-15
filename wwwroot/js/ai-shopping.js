
(() => {
    "use strict";

    const toggle = document.getElementById("es-ai-toggle");
    const panel = document.getElementById("es-ai-panel");
    const closeButton = document.getElementById("es-ai-close");
    const form = document.getElementById("es-ai-form");
    const input = document.getElementById("es-ai-input");
    const sendButton = document.getElementById("es-ai-send");
    const messages = document.getElementById("es-ai-messages");
    const productsContainer = document.getElementById("es-ai-products");

    if (!toggle || !panel || !closeButton || !form ||
        !input || !sendButton || !messages || !productsContainer) {
        console.error("Ever Spring AI widget: required HTML element is missing.");
        return;
    }

    let isSending = false;

    function openChat() {
        panel.hidden = false;
        toggle.setAttribute("aria-expanded", "true");
        input.focus();
    }

    function closeChat() {
        panel.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
    }

    toggle.addEventListener("click", () => {
        if (panel.hidden) openChat();
        else closeChat();
    });

    closeButton.addEventListener("click", closeChat);

    function addMessage(text, type) {
        const element = document.createElement("div");
        element.className = `es-ai-message ${type}`;
        element.textContent = text;
        messages.appendChild(element);
        messages.scrollTop = messages.scrollHeight;
        return element;
    }


    function renderProducts(products) {
        if (!Array.isArray(products) || products.length === 0) {
            return;
        }

        const group = document.createElement("div");
        group.className = "es-ai-product-group";

        const heading = document.createElement("p");
        heading.className = "es-ai-products-heading";
        heading.textContent = "Products you may like";
        group.appendChild(heading);

        products.forEach(product => {
            const card = document.createElement("a");
            card.className = "es-ai-product-card";
            card.href = `/Products/Details/${encodeURIComponent(product.id)}`;

            if (product.imageUrl) {
                const image = document.createElement("img");
                image.src = product.imageUrl;
                image.alt = product.name || "Product image";
                image.loading = "lazy";
                image.onerror = () => image.remove();
                card.appendChild(image);
            }

            const details = document.createElement("div");
            details.className = "es-ai-product-details";

            const name = document.createElement("strong");
            name.textContent = product.name || "Ever Spring product";

            const price = document.createElement("span");
            price.textContent = `৳${product.price ?? "—"}`;

            const stock = document.createElement("small");
            stock.textContent = `In stock: ${product.stock ?? 0}`;

            details.append(name, price, stock);
            card.appendChild(details);
            group.appendChild(card);
        });

        // Append instead of replacing previous product recommendations.
        messages.appendChild(group);
        messages.scrollTop = messages.scrollHeight;
    }


    form.addEventListener("submit", async event => {
        event.preventDefault();

        if (isSending) return;

        const message = input.value.trim();
        if (!message) {
            input.focus();
            return;
        }

        addMessage(message, "es-ai-user");
       

        input.value = "";
        input.disabled = true;
        sendButton.disabled = true;
        sendButton.textContent = "Sending...";
        isSending = true;

        const thinking = addMessage("Thinking...", "es-ai-bot");

        try {
            const formData = new FormData(form);
            formData.set("Message", message);

            const response = await fetch("/AIShopping/Chat", {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            const data = await response.json().catch(() => null);

            thinking.remove();

            if (!response.ok) {
                addMessage(
                    data?.reply ||
                    `The request failed (${response.status}). Please try again.`,
                    "es-ai-bot"
                );
                renderProducts(data?.products || []);
                return;
            }

            addMessage(
                data?.reply || "I couldn't generate a reply. Please try again.",
                "es-ai-bot"
            );

            renderProducts(data?.products || []);
        } catch (error) {
            thinking.remove();

            console.error("Ever Spring AI chat request failed:", error);

            addMessage(
                "I couldn't connect to the shop right now. Please check your connection and try again.",
                "es-ai-bot"
            );
        } finally {
            isSending = false;
            input.disabled = false;
            sendButton.disabled = false;
            sendButton.textContent = "Send ↑";
            input.focus();
        }
    });
})();

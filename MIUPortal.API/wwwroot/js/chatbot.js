// ================================
// MIU SMART ASSISTANT
// ================================

const CHATBOT_API_URL = "https://localhost:44366/api";

let chatLoaded = false;

//----------------------------------
// Load chatbot HTML
//----------------------------------

window.addEventListener("DOMContentLoaded", async () => {

    const placeholder =
        document.getElementById("chatbot-placeholder");

    if (!placeholder)
        return;

    const html =
        await fetch("components/chatbot.html");

    placeholder.innerHTML =
        await html.text();

    initialiseChat();

});

//----------------------------------
// Initialise
//----------------------------------

function initialiseChat() {

    const toggle =
        document.getElementById("chat-toggle");

    const windowDiv =
        document.getElementById("chat-window");

    const close =
        document.getElementById("close-chat");

    const send =
        document.getElementById("send-chat");

    const input =
        document.getElementById("chat-input");

    toggle.onclick = () => {

        windowDiv.style.display = "flex";

        toggle.style.display = "none";

        input.focus();

    };

    close.onclick = () => {

        windowDiv.style.display = "none";

        toggle.style.display = "block";

    };

    send.onclick = sendMessage;

    input.addEventListener("keypress", function (e) {

        if (e.key === "Enter") {

            sendMessage();

        }

    });

    //----------------------------------
    // Quick Buttons
    //----------------------------------

    document.querySelectorAll(".quick-btn")
        .forEach(btn => {

            btn.onclick = function () {

                input.value = btn.innerText;

                sendMessage();

            };

        });

}

//----------------------------------
// Send Message
//----------------------------------

async function sendMessage() {

    const input =
        document.getElementById("chat-input");

    const body =
        document.getElementById("chat-body");

    const message =
        input.value.trim();

    if (message === "")
        return;

    //----------------------------------

    addUserMessage(message);

    input.value = "";

    //----------------------------------

    const typing =
        addTypingIndicator();

    try {

        const response =
            await fetch(`${CHATBOT_API_URL}/chatbot`, {

                method: "POST",

                headers: {

                    "Content-Type":
                        "application/json"

                },

                body: JSON.stringify({

                    message: message

                })

            });

        const data =
            await response.json();

        typing.remove();

        addBotMessage(data.reply);

    }

    catch {

        typing.remove();

        addBotMessage(

            "Sorry, I couldn't connect to the AI server."

        );

    }

}

//----------------------------------
// User Message
//----------------------------------

function addUserMessage(text) {

    const body =
        document.getElementById("chat-body");

    const div =
        document.createElement("div");

    div.className =
        "user-message";

    div.innerHTML =
        text;

    body.appendChild(div);

    scrollBottom();

}

//----------------------------------
// Bot Message
//----------------------------------

function addBotMessage(text) {

    const body =
        document.getElementById("chat-body");

    const div =
        document.createElement("div");

    div.className =
        "bot-message";

    div.innerHTML =
        text.replace(/\n/g, "<br>");

    body.appendChild(div);

    scrollBottom();

}

//----------------------------------
// Typing
//----------------------------------

function addTypingIndicator() {

    const body =
        document.getElementById("chat-body");

    const div =
        document.createElement("div");

    div.className =
        "bot-message";

    div.innerHTML =

        `<div class="typing">

            <span></span>

            <span></span>

            <span></span>

        </div>`;

    body.appendChild(div);

    scrollBottom();

    return div;

}

//----------------------------------
// Scroll
//----------------------------------

function scrollBottom() {

    const body =
        document.getElementById("chat-body");

    body.scrollTop =
        body.scrollHeight;

}
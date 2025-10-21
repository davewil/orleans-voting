# Phoenix LiveView Frontend Plan - Orleans Voting

This document outlines the plan for building a Phoenix LiveView frontend for the Orleans Voting application. LiveView provides a server-rendered, real-time alternative to client-side SPAs while maintaining excellent user experience.

## Overview

Build a performant, real-time Phoenix LiveView application that consumes the REST API created in Phase 8 of the migration plan. LiveView maintains state on the server and pushes updates to the client over WebSockets, eliminating the need for complex client-side state management.

## Tech Stack

- **Language**: Elixir 1.17+ - Functional programming with excellent concurrency
- **Framework**: Phoenix 1.7+ - Modern web framework with LiveView
- **LiveView**: Phoenix LiveView 0.20+ - Server-rendered real-time UI
- **Styling**: Tailwind CSS 3.x - Utility-first CSS framework
- **HTTP Client**: Req 0.5+ or Tesla 1.11+ - Modern HTTP client for API calls
- **Database**: PostgreSQL (optional) - If adding user accounts or poll persistence
- **PubSub**: Phoenix PubSub - Real-time broadcasting built into Phoenix
- **Testing**: ExUnit - Elixir's built-in testing framework

## Why Phoenix LiveView?

### Advantages
- **Real-time by Default**: WebSocket connection maintains state, instant updates
- **Server-Side Rendering**: SEO-friendly, works without JavaScript
- **Less JavaScript**: No React bundle, faster initial page loads
- **Simpler Architecture**: No client-side state management needed
- **Phoenix PubSub**: Built-in real-time broadcasting across servers
- **Excellent Performance**: Elixir's BEAM VM handles millions of connections
- **Developer Experience**: Hot reloading, excellent error messages

### Trade-offs
- **Server Resources**: Maintains WebSocket per user (vs stateless API)
- **Latency Sensitive**: Every interaction requires server round-trip
- **Less Offline Support**: Requires active connection (though graceful degradation)
- **Different Ecosystem**: Elixir instead of JavaScript

## Prerequisites

- **Elixir**: Install via [elixir-lang.org](https://elixir-lang.org/install.html)
- **PostgreSQL**: Install via [postgresql.org](https://www.postgresql.org/download/) (optional)
- **Node.js**: Required for asset compilation (Tailwind)
- **Backend API**: Assumes OrleansVoting.Api project exists (Phase 8)
- **API Endpoints**:
  - `POST /api/polls` - Create poll
  - `GET /api/polls/{pollId}` - Get poll results
  - `POST /api/polls/{pollId}/vote` - Submit vote

## Phase 1: Project Setup and Foundation

### Step 1.1: Create New Phoenix Project

```bash
# Install Phoenix framework
mix archive.install hex phx_new

# Create new Phoenix project WITHOUT Ecto (database not required initially)
mix phx.new orleans_voting_web --no-ecto

cd orleans_voting_web

# Install dependencies
mix deps.get
```

**Project structure created**:
```
orleans_voting_web/
├── lib/
│   ├── orleans_voting_web/          # Web interface
│   │   ├── components/              # Reusable components
│   │   ├── controllers/             # HTTP controllers
│   │   └── live/                    # LiveView modules
│   └── orleans_voting_web.ex        # Web module
├── assets/                          # Frontend assets
│   ├── css/                         # Stylesheets
│   └── js/                          # JavaScript
├── config/                          # Configuration
└── test/                            # Tests
```

**Verification**: `mix phx.server` starts server at http://localhost:4000

---

### Step 1.2: Install Dependencies

**Update `mix.exs`** - Add HTTP client and utilities:
```elixir
defp deps do
  [
    {:phoenix, "~> 1.7.0"},
    {:phoenix_html, "~> 4.0"},
    {:phoenix_live_reload, "~> 1.2", only: :dev},
    {:phoenix_live_view, "~> 0.20.0"},
    {:floki, ">= 0.30.0", only: :test},
    {:esbuild, "~> 0.8", runtime: Mix.env() == :dev},
    {:tailwind, "~> 0.2", runtime: Mix.env() == :dev},
    {:heroicons,
     github: "tailwindlabs/heroicons",
     tag: "v2.1.1",
     sparse: "optimized",
     app: false,
     compile: false,
     depth: 1},
    {:telemetry_metrics, "~> 1.0"},
    {:telemetry_poller, "~> 1.0"},
    {:gettext, "~> 0.20"},
    {:jason, "~> 1.2"},
    {:dns_cluster, "~> 0.1.1"},
    {:bandit, "~> 1.2"},

    # HTTP client for API calls
    {:req, "~> 0.5.0"}
  ]
end
```

```bash
# Install dependencies
mix deps.get
```

**Verification**: `mix deps` shows all packages installed

---

### Step 1.3: Configure Tailwind CSS

Tailwind is already configured by Phoenix 1.7+, but let's customize it.

**Update `assets/tailwind.config.js`**:
```javascript
module.exports = {
  content: [
    './js/**/*.js',
    '../lib/orleans_voting_web.ex',
    '../lib/orleans_voting_web/**/*.*ex'
  ],
  theme: {
    extend: {
      colors: {
        brand: '#FD4F00',
      }
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ]
}
```

**Install Tailwind forms plugin**:
```bash
cd assets
npm install @tailwindcss/forms
cd ..
```

**Update `assets/css/app.css`**:
```css
@import "tailwindcss/base";
@import "tailwindcss/components";
@import "tailwindcss/utilities";

/* Custom styles */
.btn-primary {
  @apply px-6 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 transition-colors;
}

.btn-secondary {
  @apply px-6 py-3 bg-gray-600 text-white font-semibold rounded-lg hover:bg-gray-700 transition-colors;
}
```

**Verification**: Tailwind classes work in templates

---

### Step 1.4: Create API Client Module

**Create `lib/orleans_voting_web/api_client.ex`**:
```elixir
defmodule OrleansVotingWeb.ApiClient do
  @moduledoc """
  HTTP client for OrleansVoting REST API.
  Handles all communication with the .NET backend.
  """

  @base_url Application.compile_env(:orleans_voting_web, :api_base_url, "http://localhost:5000")

  # Response types
  @type poll_option :: %{text: String.t(), votes: integer()}
  @type poll_results :: %{
    poll_id: String.t(),
    question: String.t(),
    options: list(poll_option()),
    voted: boolean(),
    total_votes: integer()
  }

  @type create_poll_response :: %{poll_id: String.t()}
  @type api_error :: {:error, String.t()}

  @doc """
  Creates a new poll.

  ## Examples
      iex> create_poll("Favorite color?", ["Red", "Blue", "Green"])
      {:ok, %{poll_id: "abc123"}}
  """
  @spec create_poll(String.t(), list(String.t())) :: {:ok, create_poll_response()} | api_error()
  def create_poll(question, options) do
    body = %{
      question: question,
      options: options
    }

    case Req.post("#{@base_url}/api/polls", json: body) do
      {:ok, %{status: 200, body: response}} ->
        {:ok, %{poll_id: response["pollId"]}}

      {:ok, %{status: 429}} ->
        {:error, "Too many requests. Please slow down."}

      {:ok, %{status: 400, body: %{"error" => error}}} ->
        {:error, error}

      {:ok, %{status: status}} ->
        {:error, "HTTP #{status}: Unexpected error"}

      {:error, exception} ->
        {:error, Exception.message(exception)}
    end
  end

  @doc """
  Gets poll results by ID.

  ## Examples
      iex> get_poll_results("abc123")
      {:ok, %{poll_id: "abc123", question: "Test?", ...}}
  """
  @spec get_poll_results(String.t()) :: {:ok, poll_results()} | api_error()
  def get_poll_results(poll_id) do
    case Req.get("#{@base_url}/api/polls/#{poll_id}") do
      {:ok, %{status: 200, body: response}} ->
        {:ok, parse_poll_results(response)}

      {:ok, %{status: 404}} ->
        {:error, "Poll not found"}

      {:ok, %{status: status}} ->
        {:error, "HTTP #{status}: Failed to load poll"}

      {:error, exception} ->
        {:error, Exception.message(exception)}
    end
  end

  @doc """
  Submits a vote for a poll option.

  ## Examples
      iex> add_vote("abc123", 0)
      {:ok, %{poll_id: "abc123", ...}}
  """
  @spec add_vote(String.t(), integer()) :: {:ok, poll_results()} | api_error()
  def add_vote(poll_id, option_index) do
    body = %{optionIndex: option_index}

    case Req.post("#{@base_url}/api/polls/#{poll_id}/vote", json: body) do
      {:ok, %{status: 200, body: response}} ->
        {:ok, parse_poll_results(response)}

      {:ok, %{status: 400, body: %{"error" => error}}} ->
        {:error, error}

      {:ok, %{status: 429}} ->
        {:error, "Too many requests. Please slow down."}

      {:ok, %{status: status}} ->
        {:error, "HTTP #{status}: Failed to submit vote"}

      {:error, exception} ->
        {:error, Exception.message(exception)}
    end
  end

  # Private helpers

  defp parse_poll_results(response) do
    %{
      poll_id: response["pollId"],
      question: response["question"],
      options: Enum.map(response["options"], &parse_option/1),
      voted: response["voted"],
      total_votes: response["totalVotes"]
    }
  end

  defp parse_option(option) do
    %{
      text: option["text"],
      votes: option["votes"]
    }
  end
end
```

**Configure API URL in `config/config.exs`**:
```elixir
import Config

config :orleans_voting_web,
  api_base_url: System.get_env("API_BASE_URL") || "http://localhost:5000"

# ... rest of config
```

**Verification**: API client module compiles without errors

---

## Phase 2: LiveView Architecture

### Step 2.1: Update Router

**Update `lib/orleans_voting_web/router.ex`**:
```elixir
defmodule OrleansVotingWeb.Router do
  use OrleansVotingWeb, :router

  pipeline :browser do
    plug :accepts, ["html"]
    plug :fetch_session
    plug :fetch_live_flash
    plug :put_root_layout, html: {OrleansVotingWeb.Layouts, :root}
    plug :protect_from_forgery
    plug :put_secure_browser_headers
  end

  scope "/", OrleansVotingWeb do
    pipe_through :browser

    live "/", HomeLive
    live "/create", CreatePollLive
    live "/poll/:poll_id", PollLive
  end
end
```

**Verification**: Router compiles (LiveView modules don't exist yet)

---

### Step 2.2: Create Root Layout

**Update `lib/orleans_voting_web/components/layouts/root.html.heex`**:
```heex
<!DOCTYPE html>
<html lang="en" class="[scrollbar-gutter:stable]">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="csrf-token" content={get_csrf_token()} />
    <.live_title suffix=" · Orleans Voting">
      <%= assigns[:page_title] || "Orleans Voting" %>
    </.live_title>
    <link phx-track-static rel="stylesheet" href={~p"/assets/app.css"} />
    <script defer phx-track-static type="text/javascript" src={~p"/assets/app.js"}>
    </script>
  </head>
  <body class="bg-gray-50 antialiased">
    <header class="bg-white shadow-sm border-b border-gray-200">
      <div class="max-w-7xl mx-auto px-4 py-4 sm:px-6 lg:px-8">
        <div class="flex items-center justify-between">
          <.link navigate={~p"/"} class="text-2xl font-bold text-blue-600">
            Orleans Voting
          </.link>
          <nav class="space-x-4">
            <.link
              navigate={~p"/create"}
              class="text-gray-600 hover:text-gray-900 transition-colors"
            >
              Create Poll
            </.link>
          </nav>
        </div>
      </div>
    </header>

    <main class="max-w-7xl mx-auto px-4 py-8 sm:px-6 lg:px-8">
      <%= @inner_content %>
    </main>
  </body>
</html>
```

**Verification**: Layout renders with header

---

### Step 2.3: Create Home LiveView

**Create `lib/orleans_voting_web/live/home_live.ex`**:
```elixir
defmodule OrleansVotingWeb.HomeLive do
  use OrleansVotingWeb, :live_view

  @impl true
  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(:page_title, "Home")}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <div class="text-center py-16">
      <h1 class="text-4xl font-bold text-gray-900 mb-4">
        Welcome to Orleans Voting
      </h1>
      <p class="text-xl text-gray-600 mb-8">
        Create polls and gather votes in real-time with Phoenix LiveView
      </p>
      <.link navigate={~p"/create"} class="btn-primary inline-block">
        Create Your First Poll
      </.link>
    </div>
    """
  end
end
```

**Verification**:
- Run `mix phx.server`
- Navigate to http://localhost:4000
- Home page displays with working navigation

---

### Step 2.4: Create Poll Creation LiveView

**Create `lib/orleans_voting_web/live/create_poll_live.ex`**:
```elixir
defmodule OrleansVotingWeb.CreatePollLive do
  use OrleansVotingWeb, :live_view
  alias OrleansVotingWeb.ApiClient

  @impl true
  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(:page_title, "Create Poll")
     |> assign(:form, to_form(initial_changeset()))
     |> assign(:creating, false)
     |> assign(:error, nil)}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <div class="max-w-2xl mx-auto">
      <h1 class="text-3xl font-bold text-gray-900 mb-6 text-center">
        Create New Poll
      </h1>

      <.form
        for={@form}
        phx-submit="create_poll"
        phx-change="validate"
        class="bg-white rounded-lg shadow-md p-6 space-y-6"
      >
        <!-- Question Input -->
        <div>
          <.label for="question">Poll Question</.label>
          <.input
            field={@form[:question]}
            type="text"
            placeholder="What is your favorite programming language?"
            required
          />
          <.error :for={msg <- Enum.map(@form[:question].errors, &translate_error/1)}>
            <%= msg %>
          </.error>
        </div>

        <!-- Options -->
        <div>
          <.label>Options</.label>
          <.inputs_for :let={option_form} field={@form[:options]}>
            <div class="flex gap-2 mb-3">
              <.input
                field={option_form[:text]}
                type="text"
                placeholder={"Option #{option_form.index + 1}"}
                class="flex-1"
              />
              <button
                :if={option_form.index >= 2}
                type="button"
                phx-click="remove_option"
                phx-value-index={option_form.index}
                class="px-3 py-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
              >
                Remove
              </button>
            </div>
          </.inputs_for>

          <button
            type="button"
            phx-click="add_option"
            class="text-blue-600 hover:text-blue-700 font-medium text-sm"
          >
            + Add Option
          </button>
        </div>

        <!-- Error Display -->
        <div :if={@error} class="p-4 bg-red-50 border border-red-200 rounded-lg">
          <p class="text-red-800"><%= @error %></p>
        </div>

        <!-- Submit Button -->
        <button
          type="submit"
          disabled={@creating || !@form.source.valid?}
          class={[
            "w-full px-6 py-3 font-semibold rounded-lg transition-colors",
            if(@creating || !@form.source.valid?,
              do: "bg-gray-300 text-gray-500 cursor-not-allowed",
              else: "bg-blue-600 text-white hover:bg-blue-700"
            )
          ]}
        >
          <%= if @creating, do: "Creating...", else: "Create Poll" %>
        </button>
      </.form>
    </div>
    """
  end

  @impl true
  def handle_event("validate", %{"poll" => poll_params}, socket) do
    changeset =
      poll_params
      |> poll_changeset()
      |> Map.put(:action, :validate)

    {:noreply, assign(socket, :form, to_form(changeset))}
  end

  @impl true
  def handle_event("add_option", _params, socket) do
    existing = socket.assigns.form.source.changes[:options] || []
    new_options = existing ++ [%{text: ""}]

    changeset =
      socket.assigns.form.source
      |> Ecto.Changeset.put_embed(:options, new_options)

    {:noreply, assign(socket, :form, to_form(changeset))}
  end

  @impl true
  def handle_event("remove_option", %{"index" => index_str}, socket) do
    index = String.to_integer(index_str)
    existing = socket.assigns.form.source.changes[:options] || []
    new_options = List.delete_at(existing, index)

    changeset =
      socket.assigns.form.source
      |> Ecto.Changeset.put_embed(:options, new_options)

    {:noreply, assign(socket, :form, to_form(changeset))}
  end

  @impl true
  def handle_event("create_poll", %{"poll" => poll_params}, socket) do
    changeset = poll_changeset(poll_params)

    if changeset.valid? do
      question = Ecto.Changeset.get_field(changeset, :question)
      options_data = Ecto.Changeset.get_field(changeset, :options)
      options = Enum.map(options_data, & &1.text) |> Enum.filter(&(&1 != ""))

      if length(options) < 2 do
        {:noreply, assign(socket, :error, "Please provide at least 2 options")}
      else
        socket = assign(socket, :creating, true, :error, nil)

        case ApiClient.create_poll(question, options) do
          {:ok, %{poll_id: poll_id}} ->
            {:noreply, push_navigate(socket, to: ~p"/poll/#{poll_id}")}

          {:error, error} ->
            {:noreply, assign(socket, :creating, false, :error, error)}
        end
      end
    else
      {:noreply, assign(socket, :form, to_form(Map.put(changeset, :action, :validate)))}
    end
  end

  # Private helpers

  defp initial_changeset do
    poll_changeset(%{
      "question" => "",
      "options" => [%{"text" => ""}, %{"text" => ""}]
    })
  end

  defp poll_changeset(params) do
    types = %{question: :string, options: {:array, :map}}

    {%{}, types}
    |> Ecto.Changeset.cast(params, [:question, :options])
    |> Ecto.Changeset.validate_required([:question])
    |> Ecto.Changeset.validate_length(:question, min: 3, max: 500)
  end
end
```

**Note**: This uses Ecto.Changeset for form validation without a database. Install Ecto:

```bash
# Add to mix.exs deps
{:ecto, "~> 3.11"}

# Install
mix deps.get
```

**Verification**:
- Navigate to `/create`
- Fill out form
- Add/remove options
- Form validation works
- Creating poll navigates to poll page

---

### Step 2.5: Create Poll Viewing LiveView

**Create `lib/orleans_voting_web/live/poll_live.ex`**:
```elixir
defmodule OrleansVotingWeb.PollLive do
  use OrleansVotingWeb, :live_view
  alias OrleansVotingWeb.ApiClient

  @refresh_interval :timer.seconds(5)

  @impl true
  def mount(%{"poll_id" => poll_id}, _session, socket) do
    if connected?(socket) do
      # Start periodic refresh for real-time updates
      :timer.send_interval(@refresh_interval, self(), :refresh_poll)

      # Subscribe to PubSub for real-time updates (if broadcasting)
      Phoenix.PubSub.subscribe(OrleansVotingWeb.PubSub, "poll:#{poll_id}")
    end

    socket =
      socket
      |> assign(:poll_id, poll_id)
      |> assign(:loading, true)
      |> assign(:error, nil)
      |> assign(:voting, false)
      |> load_poll()

    {:ok, socket}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <div class="max-w-3xl mx-auto">
      <h1 class="text-3xl font-bold text-gray-900 mb-6 text-center">
        Poll Results
      </h1>

      <!-- Loading State -->
      <div :if={@loading} class="flex justify-center items-center py-12">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>

      <!-- Error State -->
      <div :if={@error && !@loading} class="bg-red-50 border border-red-200 rounded-lg p-6">
        <p class="text-red-800 font-medium">Failed to load poll</p>
        <p class="text-red-600 text-sm mt-1"><%= @error %></p>
      </div>

      <!-- Poll Content -->
      <div :if={assigns[:poll] && !@loading} class="bg-white rounded-lg shadow-md p-6 space-y-6">
        <!-- Poll Question -->
        <div>
          <h2 class="text-2xl font-bold text-gray-900 mb-2"><%= @poll.question %></h2>
          <p class="text-sm text-gray-500">
            Total votes: <%= @poll.total_votes %>
            <span :if={@poll.voted} class="ml-2 text-green-600">✓ You voted</span>
          </p>
        </div>

        <!-- Options -->
        <div class="space-y-3">
          <%= for {option, index} <- Enum.with_index(@poll.options) do %>
            <% percentage =
              if @poll.total_votes > 0,
                do: round(option.votes / @poll.total_votes * 100),
                else: 0 %>

            <button
              phx-click="vote"
              phx-value-index={index}
              disabled={@poll.voted || @voting}
              class={[
                "w-full text-left p-4 rounded-lg border-2 transition-all",
                if(@poll.voted,
                  do: "cursor-default",
                  else: "hover:border-blue-500 hover:shadow-md cursor-pointer"
                ),
                if(@voting, do: "opacity-50 cursor-wait")
              ]}
            >
              <div class="flex justify-between items-center mb-2">
                <span class="font-medium text-gray-900"><%= option.text %></span>
                <span class="text-sm font-semibold text-gray-600">
                  <%= option.votes %> votes (<%= percentage %>%)
                </span>
              </div>

              <!-- Progress Bar -->
              <div class="w-full bg-gray-200 rounded-full h-2">
                <div
                  class="bg-blue-600 h-2 rounded-full transition-all duration-500"
                  style={"width: #{percentage}%"}
                >
                </div>
              </div>
            </button>
          <% end %>
        </div>

        <!-- Share Link -->
        <div class="pt-4 border-t border-gray-200">
          <p class="text-sm text-gray-600 mb-2">Share this poll:</p>
          <div class="flex gap-2">
            <input
              type="text"
              readonly
              value={url(~p"/poll/#{@poll_id}")}
              id="share-link"
              class="flex-1 px-3 py-2 bg-gray-50 border border-gray-300 rounded text-sm"
              phx-hook="SelectOnClick"
            />
            <button
              phx-click={JS.dispatch("phx:copy", to: "#share-link")}
              class="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700 transition-colors text-sm"
            >
              Copy
            </button>
          </div>
        </div>
      </div>
    </div>
    """
  end

  @impl true
  def handle_event("vote", %{"index" => index_str}, socket) do
    index = String.to_integer(index_str)

    socket = assign(socket, :voting, true)

    case ApiClient.add_vote(socket.assigns.poll_id, index) do
      {:ok, poll_results} ->
        {:noreply,
         socket
         |> assign(:poll, poll_results)
         |> assign(:voting, false)
         |> put_flash(:info, "Vote submitted successfully!")}

      {:error, error} ->
        {:noreply,
         socket
         |> assign(:voting, false)
         |> put_flash(:error, error)}
    end
  end

  @impl true
  def handle_info(:refresh_poll, socket) do
    {:noreply, load_poll(socket)}
  end

  @impl true
  def handle_info({:poll_updated, poll_results}, socket) do
    # Received from PubSub broadcast (if implemented)
    {:noreply, assign(socket, :poll, poll_results, :loading, false)}
  end

  # Private helpers

  defp load_poll(socket) do
    case ApiClient.get_poll_results(socket.assigns.poll_id) do
      {:ok, poll_results} ->
        socket
        |> assign(:poll, poll_results)
        |> assign(:loading, false)
        |> assign(:error, nil)

      {:error, error} ->
        socket
        |> assign(:loading, false)
        |> assign(:error, error)
    end
  end

  defp url(path) do
    OrleansVotingWeb.Endpoint.url() <> path
  end
end
```

**Add copy-to-clipboard hook in `assets/js/app.js`**:
```javascript
let Hooks = {}

Hooks.SelectOnClick = {
  mounted() {
    this.el.addEventListener('click', e => {
      e.target.select()
    })
  }
}

// Add to LiveSocket
let liveSocket = new LiveSocket("/live", Socket, {
  params: {_csrf_token: csrfToken},
  hooks: Hooks
})
```

**Verification**:
- Create poll at `/create`
- Navigate to poll page
- Vote on poll
- Results update every 5 seconds
- Copy link works

---

## Phase 3: Real-Time Updates with Phoenix PubSub

### Step 3.1: Create PubSub Broadcaster (Optional)

If you want instant updates instead of polling, create a GenServer that polls the API and broadcasts changes.

**Create `lib/orleans_voting_web/poll_broadcaster.ex`**:
```elixir
defmodule OrleansVotingWeb.PollBroadcaster do
  @moduledoc """
  GenServer that polls the API for poll updates and broadcasts changes via PubSub.
  This allows all connected LiveView clients to receive instant updates.
  """
  use GenServer
  alias OrleansVotingWeb.ApiClient
  alias Phoenix.PubSub

  @poll_interval :timer.seconds(2)

  def start_link(poll_id) do
    GenServer.start_link(__MODULE__, poll_id, name: via_tuple(poll_id))
  end

  def watch_poll(poll_id) do
    case GenServer.whereis(via_tuple(poll_id)) do
      nil ->
        # Start broadcaster if not running
        DynamicSupervisor.start_child(
          OrleansVotingWeb.PollBroadcasterSupervisor,
          {__MODULE__, poll_id}
        )

      _pid ->
        :ok
    end
  end

  @impl true
  def init(poll_id) do
    schedule_poll()

    {:ok,
     %{
       poll_id: poll_id,
       last_results: nil
     }}
  end

  @impl true
  def handle_info(:poll_api, state) do
    case ApiClient.get_poll_results(state.poll_id) do
      {:ok, results} ->
        # Broadcast if results changed
        if results != state.last_results do
          PubSub.broadcast(
            OrleansVotingWeb.PubSub,
            "poll:#{state.poll_id}",
            {:poll_updated, results}
          )
        end

        schedule_poll()
        {:noreply, %{state | last_results: results}}

      {:error, _} ->
        schedule_poll()
        {:noreply, state}
    end
  end

  defp schedule_poll do
    Process.send_after(self(), :poll_api, @poll_interval)
  end

  defp via_tuple(poll_id) do
    {:via, Registry, {OrleansVotingWeb.PollRegistry, poll_id}}
  end
end
```

**Create supervisor in `lib/orleans_voting_web/application.ex`**:
```elixir
defmodule OrleansVotingWeb.Application do
  use Application

  @impl true
  def start(_type, _args) do
    children = [
      OrleansVotingWeb.Telemetry,
      {Phoenix.PubSub, name: OrleansVotingWeb.PubSub},

      # Registry for poll broadcasters
      {Registry, keys: :unique, name: OrleansVotingWeb.PollRegistry},

      # Dynamic supervisor for poll broadcasters
      {DynamicSupervisor, name: OrleansVotingWeb.PollBroadcasterSupervisor, strategy: :one_for_one},

      OrleansVotingWeb.Endpoint
    ]

    opts = [strategy: :one_for_one, name: OrleansVotingWeb.Supervisor]
    Supervisor.start_link(children, opts)
  end

  @impl true
  def config_change(changed, _new, removed) do
    OrleansVotingWeb.Endpoint.config_change(changed, removed)
    :ok
  end
end
```

**Update `PollLive` mount to start broadcaster**:
```elixir
def mount(%{"poll_id" => poll_id}, _session, socket) do
  if connected?(socket) do
    # Start broadcaster for this poll
    OrleansVotingWeb.PollBroadcaster.watch_poll(poll_id)

    # Subscribe to updates
    Phoenix.PubSub.subscribe(OrleansVotingWeb.PubSub, "poll:#{poll_id}")
  end

  # ... rest unchanged, remove :timer.send_interval
end
```

**Verification**:
- Open poll in two browser windows
- Vote in one window
- Other window updates within 2 seconds via PubSub

---

## Phase 4: Advanced Features

### Step 4.1: Add Loading Skeletons

**Create `lib/orleans_voting_web/components/poll_components.ex`**:
```elixir
defmodule OrleansVotingWeb.PollComponents do
  use Phoenix.Component

  def poll_skeleton(assigns) do
    ~H"""
    <div class="max-w-3xl mx-auto">
      <div class="bg-white rounded-lg shadow-md p-6 space-y-6 animate-pulse">
        <div class="h-8 bg-gray-200 rounded w-3/4"></div>
        <div class="h-4 bg-gray-200 rounded w-1/4"></div>
        <div class="space-y-3">
          <%= for _i <- 1..3 do %>
            <div class="p-4 rounded-lg border-2 border-gray-200">
              <div class="h-6 bg-gray-200 rounded w-1/2 mb-2"></div>
              <div class="h-2 bg-gray-200 rounded"></div>
            </div>
          <% end %>
        </div>
      </div>
    </div>
    """
  end

  def option_card(assigns) do
    ~H"""
    <button
      phx-click={@on_click}
      disabled={@disabled}
      class={[
        "w-full text-left p-4 rounded-lg border-2 transition-all",
        if(@disabled, do: "cursor-default", else: "hover:border-blue-500 hover:shadow-md")
      ]}
    >
      <div class="flex justify-between items-center mb-2">
        <span class="font-medium text-gray-900"><%= @option.text %></span>
        <span class="text-sm font-semibold text-gray-600">
          <%= @option.votes %> votes (<%= @percentage %>%)
        </span>
      </div>

      <div class="w-full bg-gray-200 rounded-full h-2">
        <div
          class="bg-blue-600 h-2 rounded-full transition-all duration-500"
          style={"width: #{@percentage}%"}
        >
        </div>
      </div>
    </button>
    """
  end
end
```

**Use in `PollLive`**:
```elixir
def render(assigns) do
  ~H"""
  <div class="max-w-3xl mx-auto">
    <h1 class="text-3xl font-bold text-gray-900 mb-6 text-center">
      Poll Results
    </h1>

    <.poll_skeleton :if={@loading} />
    <!-- ... rest of template -->
  </div>
  """
end
```

---

### Step 4.2: Add Recent Polls List (Optional)

If API supports listing recent polls:

**Create `lib/orleans_voting_web/live/polls_live.ex`**:
```elixir
defmodule OrleansVotingWeb.PollsLive do
  use OrleansVotingWeb, :live_view

  @impl true
  def mount(_params, _session, socket) do
    {:ok,
     socket
     |> assign(:page_title, "Recent Polls")
     |> assign(:loading, true)
     |> load_recent_polls()}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <div>
      <h1 class="text-3xl font-bold text-gray-900 mb-6">Recent Polls</h1>

      <div :if={@loading} class="text-gray-600">Loading...</div>

      <div :if={!@loading && assigns[:polls]} class="grid gap-4">
        <%= for poll <- @polls do %>
          <.link
            navigate={~p"/poll/#{poll.poll_id}"}
            class="block p-6 bg-white rounded-lg shadow hover:shadow-md transition-shadow"
          >
            <h3 class="font-semibold text-lg"><%= poll.question %></h3>
            <p class="text-gray-600 text-sm mt-1"><%= poll.total_votes %> votes</p>
          </.link>
        <% end %>
      </div>
    </div>
    """
  end

  defp load_recent_polls(socket) do
    # Assumes API endpoint exists
    case ApiClient.get_recent_polls() do
      {:ok, polls} -> assign(socket, :polls, polls, :loading, false)
      {:error, _} -> assign(socket, :loading, false)
    end
  end
end
```

---

### Step 4.3: Add "Demo Autofill" for Testing

**Update `CreatePollLive` to add demo button**:
```elixir
def render(assigns) do
  ~H"""
  <div class="max-w-2xl mx-auto">
    <div class="flex justify-between items-center mb-6">
      <h1 class="text-3xl font-bold text-gray-900">Create New Poll</h1>
      <button
        type="button"
        phx-click="demo_autofill"
        class="text-sm text-gray-600 hover:text-gray-900 underline"
      >
        Demo Autofill
      </button>
    </div>
    <!-- ... rest of form -->
  </div>
  """
end

def handle_event("demo_autofill", _params, socket) do
  changeset =
    poll_changeset(%{
      "question" => "What is your favorite programming language?",
      "options" => [
        %{"text" => "Elixir"},
        %{"text" => "Rust"},
        %{"text" => "Go"},
        %{"text" => "TypeScript"}
      ]
    })

  {:noreply, assign(socket, :form, to_form(changeset))}
end
```

---

## Phase 5: Testing Strategy

### Step 5.1: Unit Tests for API Client

**Create `test/orleans_voting_web/api_client_test.exs`**:
```elixir
defmodule OrleansVotingWeb.ApiClientTest do
  use ExUnit.Case, async: true
  alias OrleansVotingWeb.ApiClient

  # These tests require a running API server
  # In production, you'd use mocks or bypass

  @moduletag :integration

  describe "create_poll/2" do
    test "creates poll and returns poll ID" do
      {:ok, %{poll_id: poll_id}} = ApiClient.create_poll("Test?", ["A", "B"])

      assert is_binary(poll_id)
      assert String.length(poll_id) == 6
    end

    test "returns error for invalid input" do
      {:error, error} = ApiClient.create_poll("", [])
      assert is_binary(error)
    end
  end

  describe "get_poll_results/1" do
    test "retrieves poll results" do
      {:ok, %{poll_id: poll_id}} = ApiClient.create_poll("Test?", ["A", "B"])
      {:ok, results} = ApiClient.get_poll_results(poll_id)

      assert results.poll_id == poll_id
      assert results.question == "Test?"
      assert length(results.options) == 2
      assert results.total_votes == 0
    end

    test "returns error for non-existent poll" do
      {:error, error} = ApiClient.get_poll_results("invalid")
      assert error == "Poll not found"
    end
  end

  describe "add_vote/2" do
    test "submits vote successfully" do
      {:ok, %{poll_id: poll_id}} = ApiClient.create_poll("Test?", ["A", "B"])
      {:ok, results} = ApiClient.add_vote(poll_id, 0)

      assert results.total_votes == 1
      assert results.voted == true
      assert Enum.at(results.options, 0).votes == 1
    end

    test "returns error for double voting" do
      {:ok, %{poll_id: poll_id}} = ApiClient.create_poll("Test?", ["A", "B"])
      {:ok, _} = ApiClient.add_vote(poll_id, 0)
      {:error, error} = ApiClient.add_vote(poll_id, 1)

      assert error =~ "already voted"
    end
  end
end
```

**Run tests**:
```bash
# Run all tests except integration
mix test

# Run integration tests (requires API server running)
mix test --only integration
```

---

### Step 5.2: LiveView Integration Tests

**Create `test/orleans_voting_web/live/poll_live_test.exs`**:
```elixir
defmodule OrleansVotingWeb.PollLiveTest do
  use OrleansVotingWeb.ConnCase
  import Phoenix.LiveViewTest

  @moduletag :integration

  test "displays poll and allows voting", %{conn: conn} do
    # Create poll via API
    {:ok, %{poll_id: poll_id}} =
      OrleansVotingWeb.ApiClient.create_poll("Favorite color?", ["Red", "Blue"])

    # Visit poll page
    {:ok, view, _html} = live(conn, ~p"/poll/#{poll_id}")

    # Assert poll question displayed
    assert render(view) =~ "Favorite color?"
    assert render(view) =~ "Red"
    assert render(view) =~ "Blue"

    # Submit vote
    view
    |> element("button", "Red")
    |> render_click()

    # Assert vote counted
    assert render(view) =~ "✓ You voted"
    assert render(view) =~ "1 votes"
  end

  test "handles non-existent poll", %{conn: conn} do
    {:ok, view, _html} = live(conn, ~p"/poll/invalid")

    assert render(view) =~ "Failed to load poll"
  end
end
```

---

### Step 5.3: Component Tests

**Create `test/orleans_voting_web/live/create_poll_live_test.exs`**:
```elixir
defmodule OrleansVotingWeb.CreatePollLiveTest do
  use OrleansVotingWeb.ConnCase
  import Phoenix.LiveViewTest

  test "renders poll creation form", %{conn: conn} do
    {:ok, view, _html} = live(conn, ~p"/create")

    assert render(view) =~ "Create New Poll"
    assert render(view) =~ "Poll Question"
  end

  test "validates required fields", %{conn: conn} do
    {:ok, view, _html} = live(conn, ~p"/create")

    # Submit empty form
    view
    |> form("#poll-form", poll: %{question: "", options: []})
    |> render_submit()

    assert render(view) =~ "can&#39;t be blank"
  end

  @tag :integration
  test "creates poll and redirects", %{conn: conn} do
    {:ok, view, _html} = live(conn, ~p"/create")

    # Fill and submit form
    view
    |> form("#poll-form",
      poll: %{
        question: "Test?",
        options: [%{text: "A"}, %{text: "B"}]
      }
    )
    |> render_submit()

    # Should redirect to poll page
    assert_redirect(view, ~r"/poll/[a-zA-Z0-9]+")
  end
end
```

---

## Phase 6: Deployment

### Step 6.1: Environment Configuration

**Create `config/runtime.exs`**:
```elixir
import Config

if config_env() == :prod do
  # API URL from environment
  config :orleans_voting_web,
    api_base_url: System.get_env("API_BASE_URL") || raise("API_BASE_URL not set")

  # Secret key base
  secret_key_base =
    System.get_env("SECRET_KEY_BASE") ||
      raise "environment variable SECRET_KEY_BASE is missing"

  host = System.get_env("PHX_HOST") || "example.com"
  port = String.to_integer(System.get_env("PORT") || "4000")

  config :orleans_voting_web, OrleansVotingWeb.Endpoint,
    url: [host: host, port: 443, scheme: "https"],
    http: [
      ip: {0, 0, 0, 0, 0, 0, 0, 0},
      port: port
    ],
    secret_key_base: secret_key_base
end
```

---

### Step 6.2: Build Release

**Build production release**:
```bash
# Build assets
mix assets.deploy

# Build release
MIX_ENV=prod mix release

# Run release
API_BASE_URL=http://api.example.com \
SECRET_KEY_BASE=$(mix phx.gen.secret) \
PHX_HOST=example.com \
_build/prod/rel/orleans_voting_web/bin/orleans_voting_web start
```

**Verification**: Production release runs successfully

---

### Step 6.3: Docker Deployment

**Create `Dockerfile`**:
```dockerfile
FROM elixir:1.17-alpine AS builder

# Install build dependencies
RUN apk add --no-cache build-base npm git

WORKDIR /app

# Install hex and rebar
RUN mix local.hex --force && \
    mix local.rebar --force

# Set build ENV
ENV MIX_ENV=prod

# Install dependencies
COPY mix.exs mix.lock ./
RUN mix deps.get --only $MIX_ENV
RUN mix deps.compile

# Build assets
COPY assets assets
COPY priv priv
RUN mix assets.deploy

# Compile and build release
COPY lib lib
COPY config config
RUN mix compile
RUN mix release

# Start a new build stage (smaller image)
FROM alpine:3.18 AS app

RUN apk add --no-cache openssl ncurses-libs

WORKDIR /app

RUN chown nobody:nobody /app

USER nobody:nobody

COPY --from=builder --chown=nobody:nobody /app/_build/prod/rel/orleans_voting_web ./

ENV HOME=/app

CMD ["bin/orleans_voting_web", "start"]
```

**Create `.dockerignore`**:
```
_build
deps
.git
.gitignore
test
priv/static
```

**Build and run**:
```bash
docker build -t orleans-voting-web .

docker run -p 4000:4000 \
  -e API_BASE_URL=http://api.example.com \
  -e SECRET_KEY_BASE=$(mix phx.gen.secret) \
  -e PHX_HOST=example.com \
  orleans-voting-web
```

---

### Step 6.4: Integration with .NET Aspire (Optional)

While .NET Aspire doesn't natively support Elixir/Phoenix, you can run it as a container resource.

**Update `OrleansVoting.AppHost/Program.cs`**:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Existing services
var redis = builder.AddRedis("voting-redis");

var silo = builder.AddProject<Projects.OrleansVoting_Silo>("silo")
    .WithReference(redis)
    .WithReplicas(3);

var api = builder.AddProject<Projects.OrleansVoting_Api>("api")
    .WithReference(redis);

// Add Phoenix LiveView frontend (runs in Docker container)
var phoenixWeb = builder.AddContainer("phoenix-web", "orleans-voting-web")
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("http"))
    .WithEnvironment("SECRET_KEY_BASE", builder.Configuration["Phoenix:SecretKeyBase"] ?? "dev-secret")
    .WithEnvironment("PHX_HOST", "localhost")
    .WithHttpEndpoint(port: 4000, targetPort: 4000);

builder.Build().Run();
```

**Or run separately**:
```bash
# Terminal 1: Run Aspire (API + Silo)
cd OrleansVoting.AppHost
dotnet run

# Terminal 2: Run Phoenix
cd orleans_voting_web
API_BASE_URL=http://localhost:5000 mix phx.server
```

---

## Implementation Checklist

### Phase 1: Foundation
- [ ] Create Phoenix project with `mix phx.new`
- [ ] Install dependencies (Req, Ecto for changesets)
- [ ] Configure Tailwind CSS
- [ ] Create API client module with type specs
- [ ] Configure environment variables

### Phase 2: LiveView Architecture
- [ ] Update router with LiveView routes
- [ ] Create root layout with header/navigation
- [ ] Build HomeLive (landing page)
- [ ] Build CreatePollLive (form with validation)
- [ ] Build PollLive (voting and results)
- [ ] Test all routes and navigation

### Phase 3: Real-Time Updates
- [ ] Add periodic polling (5-second interval)
- [ ] Create PollBroadcaster GenServer (optional)
- [ ] Setup Registry and DynamicSupervisor
- [ ] Integrate PubSub broadcasting
- [ ] Test real-time updates across multiple windows

### Phase 4: Advanced Features
- [ ] Add loading skeletons
- [ ] Add recent polls list (if API supports)
- [ ] Add demo autofill button
- [ ] Add copy-to-clipboard functionality

### Phase 5: Testing
- [ ] Write API client unit tests
- [ ] Write LiveView integration tests
- [ ] Write component tests
- [ ] Setup test helpers and fixtures

### Phase 6: Deployment
- [ ] Configure production environment
- [ ] Build production release
- [ ] Create Dockerfile
- [ ] Test Docker deployment
- [ ] Integrate with Aspire or deploy separately

---

## Phoenix LiveView vs React Comparison

| Feature | Phoenix LiveView | React (TanStack) |
|---------|------------------|------------------|
| **Language** | Elixir (server) | TypeScript (client) |
| **State Location** | Server | Client |
| **Real-time** | Built-in (WebSocket) | Requires SignalR |
| **Bundle Size** | ~50KB | ~200KB+ |
| **SEO** | Excellent (server-rendered) | Good (with SSR) |
| **Offline Support** | Limited | Excellent (with service workers) |
| **Server Load** | 1 process per connection | Stateless API calls |
| **Developer Experience** | Single language (Elixir) | Dual language (TS + C#) |
| **Ecosystem** | Elixir/Phoenix | JavaScript/React |
| **Mobile App** | Requires separate app | React Native reuse |

---

## Performance Considerations

### Scaling LiveView
- **Process Per Connection**: Each LiveView maintains a GenServer process
  - 1,000 users = 1,000 processes
  - BEAM VM handles millions of processes efficiently
  - ~2KB memory per process

- **Horizontal Scaling**: Add more Phoenix nodes
  - Use Phoenix PubSub with Redis adapter for cross-node communication
  - No session state stored in memory (stateless cookies)

- **Optimization Tips**:
  - Use `assign_new/3` to prevent re-assignment
  - Minimize data sent over wire with `:temporary_assigns`
  - Use LiveView streams for large lists

### Example Optimization

**Efficient Poll Rendering**:
```elixir
def mount(_params, _session, socket) do
  socket =
    socket
    |> assign(:page_title, "Poll")
    |> assign_new(:poll, fn -> load_poll() end)  # Only assign if not exists

  {:ok, socket, temporary_assigns: [flash: nil]}  # Clear flash after render
end
```

---

## Advanced Topics (Future Enhancements)

### 1. Add User Authentication
```bash
mix phx.gen.auth Accounts User users
```

### 2. Add Database Persistence
```elixir
# Store polls in PostgreSQL instead of relying solely on API
defmodule OrleansVotingWeb.Polls.Poll do
  use Ecto.Schema

  schema "polls" do
    field :poll_id, :string
    field :question, :string
    field :voted, :boolean, default: false

    timestamps()
  end
end
```

### 3. Add GraphQL API Support
```bash
mix deps.add absinthe absinthe_plug
```

### 4. Add LiveView Uploads
For poll image attachments:
```elixir
def render(assigns) do
  ~H"""
  <form phx-change="validate" phx-submit="save">
    <.live_file_input upload={@uploads.avatar} />
  </form>
  """
end
```

### 5. Add Presence Tracking
Show who's currently viewing a poll:
```elixir
defmodule OrleansVotingWeb.Presence do
  use Phoenix.Presence,
    otp_app: :orleans_voting_web,
    pubsub_server: OrleansVotingWeb.PubSub
end
```

---

## Resources

- [Phoenix Framework](https://www.phoenixframework.org/)
- [Phoenix LiveView Docs](https://hexdocs.pm/phoenix_live_view)
- [Elixir School](https://elixirschool.com/)
- [Req HTTP Client](https://hexdocs.pm/req)
- [Tailwind CSS](https://tailwindcss.com/)
- [Phoenix LiveView Course](https://pragmaticstudio.com/phoenix-liveview)

---

## Next Steps After Completion

Once the Phoenix LiveView frontend is complete, consider:

1. **Mobile App**: ElixirConf mobile app built with LiveView Native
2. **Admin Dashboard**: LiveView-powered admin with live metrics
3. **Multi-tenancy**: Support multiple organizations with separate polls
4. **Analytics**: Track poll performance with Telemetry
5. **Internationalization**: Use Gettext for multi-language support
6. **Progressive Enhancement**: Add service worker for offline form drafts

---

## Conclusion

Phoenix LiveView provides a compelling alternative to React SPAs, especially for real-time applications like voting systems. The server-rendered approach eliminates much of the complexity of client-side state management while maintaining excellent user experience.

**Choose Phoenix LiveView if**:
- You value simplicity and single-language development
- Real-time updates are core to your application
- You want excellent SEO out of the box
- Your team knows Elixir or wants to learn

**Choose React if**:
- You need offline-first capabilities
- You're building a mobile app with React Native
- Your team is already JavaScript-focused
- You need fine-grained client-side control

Both approaches can coexist - the API layer makes it possible to support multiple frontend technologies simultaneously!

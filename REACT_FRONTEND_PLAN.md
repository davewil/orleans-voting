# React Frontend Plan - Orleans Voting

This document outlines the plan for building a modern React frontend for the Orleans Voting application using Vite, TypeScript, Tailwind CSS, and the TanStack ecosystem.

## Overview

Build a performant, type-safe React SPA that consumes the REST API created in Phase 8 of the migration plan. The frontend will replace the existing Blazor UI while maintaining all functionality: poll creation, voting, real-time results, and user session management.

## Tech Stack

- **Build Tool**: Vite 6.x - Fast dev server with HMR, optimized production builds
- **Framework**: React 18+ - Modern hooks-based architecture
- **Language**: TypeScript 5.x - Type safety throughout
- **Styling**: Tailwind CSS 4.x - Utility-first CSS framework
- **Routing**: TanStack Router - Type-safe routing with search params and loaders
- **Data Fetching**: TanStack Query - Server state management with caching and automatic refetching
- **Additional TanStack Libraries** (as needed):
  - TanStack Table - Advanced data tables if needed for admin features
  - TanStack Form - Type-safe form handling with validation
  - TanStack Virtual - Virtualized lists for large datasets

## Prerequisites

- **Backend API**: Assumes OrleansVoting.Api project exists (Phase 8)
- **API Endpoints**:
  - `POST /api/polls` - Create poll
  - `GET /api/polls/{pollId}` - Get poll results
  - `POST /api/polls/{pollId}/vote` - Submit vote
  - (Optional) SignalR hub at `/pollHub` for real-time updates

## Phase 1: Project Setup and Foundation

### Step 1.1: Initialize Vite Project

```bash
# Create new Vite + React + TypeScript project
npm create vite@latest OrleansVoting.Web -- --template react-ts

cd OrleansVoting.Web

# Install dependencies
npm install
```

**Verification**: `npm run dev` starts dev server at http://localhost:5173

---

### Step 1.2: Install Core Dependencies

```bash
# TanStack ecosystem
npm install @tanstack/react-router @tanstack/react-query

# TanStack Router devtools and CLI
npm install -D @tanstack/router-devtools @tanstack/router-cli

# Tailwind CSS
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

# Additional utilities
npm install clsx tailwind-merge  # For className utilities
npm install @tanstack/react-query-devtools  # Dev tools
```

**Create `src/lib/utils.ts`** - Tailwind class merge utility:
```typescript
import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
```

**Update `tailwind.config.js`**:
```javascript
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
```

**Update `src/index.css`**:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

**Verification**: Tailwind classes work in components

---

### Step 1.3: Configure TypeScript for TanStack Router

TanStack Router generates route types for full type safety.

**Update `tsconfig.json`** - Add paths for route generation:
```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "paths": {
      "~/*": ["./src/*"]
    }
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

**Update `vite.config.ts`** - Add path alias and TanStack Router plugin:
```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { TanStackRouterVite } from '@tanstack/router-plugin/vite'
import path from 'path'

export default defineConfig({
  plugins: [
    TanStackRouterVite(),  // Must be before react()
    react(),
  ],
  resolve: {
    alias: {
      '~': path.resolve(__dirname, './src'),
    },
  },
})
```

**Verification**: TypeScript compiles without errors

---

### Step 1.4: Setup API Client with Type Definitions

**Create `src/lib/api-types.ts`** - API contract types:
```typescript
// Request types
export interface CreatePollRequest {
  question: string
  options: string[]
}

export interface AddVoteRequest {
  optionIndex: number
}

// Response types
export interface CreatePollResponse {
  pollId: string
}

export interface PollOption {
  text: string
  votes: number
}

export interface PollResultsResponse {
  pollId: string
  question: string
  options: PollOption[]
  voted: boolean
  totalVotes: number
}

// Error types
export interface ApiError {
  error: string
  status?: number
}
```

**Create `src/lib/api-client.ts`** - API client with error handling:
```typescript
import type {
  CreatePollRequest,
  CreatePollResponse,
  PollResultsResponse,
  AddVoteRequest,
  ApiError,
} from './api-types'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

class ApiClient {
  private async request<T>(
    endpoint: string,
    options?: RequestInit
  ): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
    })

    if (!response.ok) {
      if (response.status === 429) {
        throw new Error('Too many requests. Please slow down.')
      }

      const error: ApiError = await response.json().catch(() => ({
        error: `HTTP ${response.status}: ${response.statusText}`,
        status: response.status,
      }))

      throw new Error(error.error)
    }

    return response.json()
  }

  async createPoll(request: CreatePollRequest): Promise<CreatePollResponse> {
    return this.request<CreatePollResponse>('/api/polls', {
      method: 'POST',
      body: JSON.stringify(request),
    })
  }

  async getPollResults(pollId: string): Promise<PollResultsResponse> {
    return this.request<PollResultsResponse>(`/api/polls/${pollId}`)
  }

  async addVote(pollId: string, request: AddVoteRequest): Promise<PollResultsResponse> {
    return this.request<PollResultsResponse>(`/api/polls/${pollId}/vote`, {
      method: 'POST',
      body: JSON.stringify(request),
    })
  }
}

export const apiClient = new ApiClient()
```

**Create `.env`** - Environment variables:
```
VITE_API_URL=http://localhost:5000
```

**Verification**: API client compiles with full type safety

---

## Phase 2: Routing Architecture with TanStack Router

### Step 2.1: Define Route Structure

```
/ (root)
├── / (index) - Home page with "Create Poll" button
├── /create - Poll creation form
└── /poll/$pollId - Poll voting and results page
```

**Create `src/routes/__root.tsx`** - Root layout:
```typescript
import { createRootRoute, Link, Outlet } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/router-devtools'

export const Route = createRootRoute({
  component: RootLayout,
})

function RootLayout() {
  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between">
            <Link to="/" className="text-2xl font-bold text-blue-600">
              Orleans Voting
            </Link>
            <nav className="space-x-4">
              <Link
                to="/create"
                className="text-gray-600 hover:text-gray-900 transition-colors"
              >
                Create Poll
              </Link>
            </nav>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 py-8 sm:px-6 lg:px-8">
        <Outlet />
      </main>

      <TanStackRouterDevtools position="bottom-right" />
    </div>
  )
}
```

**Create `src/routes/index.tsx`** - Home page:
```typescript
import { createFileRoute, Link } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: IndexPage,
})

function IndexPage() {
  return (
    <div className="text-center py-16">
      <h1 className="text-4xl font-bold text-gray-900 mb-4">
        Welcome to Orleans Voting
      </h1>
      <p className="text-xl text-gray-600 mb-8">
        Create polls and gather votes in real-time
      </p>
      <Link
        to="/create"
        className="inline-flex items-center px-6 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 transition-colors"
      >
        Create Your First Poll
      </Link>
    </div>
  )
}
```

**Create `src/routes/create.tsx`** - Poll creation page (placeholder):
```typescript
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/create')({
  component: CreatePollPage,
})

function CreatePollPage() {
  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-6">Create New Poll</h1>
      {/* Form will be added in Phase 3 */}
    </div>
  )
}
```

**Create `src/routes/poll/$pollId.tsx`** - Poll voting page (placeholder):
```typescript
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/poll/$pollId')({
  component: PollPage,
})

function PollPage() {
  const { pollId } = Route.useParams()

  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-6">Poll: {pollId}</h1>
      {/* Poll details will be added in Phase 3 */}
    </div>
  )
}
```

---

### Step 2.2: Setup Router and Query Client

**Create `src/main.tsx`** - Application entry point:
```typescript
import React from 'react'
import ReactDOM from 'react-dom/client'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import './index.css'

// Import generated route tree
import { routeTree } from './routeTree.gen'

// Create router instance
const router = createRouter({ routeTree })

// Register router for type safety
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

// Create query client with sensible defaults
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 10, // 10 seconds
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
})

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </React.StrictMode>
)
```

**Update `package.json`** - Add route generation script:
```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "lint": "eslint . --ext ts,tsx --report-unused-disable-directives --max-warnings 0",
    "preview": "vite preview",
    "routes": "tsr generate"
  }
}
```

**Verification**:
- Run `npm run routes` to generate route types
- Run `npm run dev` to start dev server
- Navigate to `/`, `/create`, and `/poll/test-123` - all routes work
- TanStack Router Devtools visible in bottom-right corner

---

## Phase 3: Data Management with TanStack Query

### Step 3.1: Create Query Hooks

**Create `src/hooks/use-polls.ts`** - Poll query hooks:
```typescript
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { apiClient } from '~/lib/api-client'
import type { CreatePollRequest, AddVoteRequest } from '~/lib/api-types'

// Query keys for cache management
export const pollKeys = {
  all: ['polls'] as const,
  detail: (pollId: string) => ['polls', pollId] as const,
}

// Fetch poll results
export function usePollResults(pollId: string) {
  return useQuery({
    queryKey: pollKeys.detail(pollId),
    queryFn: () => apiClient.getPollResults(pollId),
    refetchInterval: 5000, // Poll every 5 seconds for real-time updates
  })
}

// Create poll mutation
export function useCreatePoll() {
  const navigate = useNavigate()

  return useMutation({
    mutationFn: (request: CreatePollRequest) => apiClient.createPoll(request),
    onSuccess: (data) => {
      // Navigate to new poll page
      navigate({ to: '/poll/$pollId', params: { pollId: data.pollId } })
    },
  })
}

// Add vote mutation
export function useAddVote(pollId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: AddVoteRequest) => apiClient.addVote(pollId, request),
    onSuccess: (data) => {
      // Optimistically update cache with new results
      queryClient.setQueryData(pollKeys.detail(pollId), data)
    },
  })
}
```

**Verification**: Hooks compile with full type safety

---

### Step 3.2: Build Poll Creation Form

**Option A: Using TanStack Form (Recommended)**

Install TanStack Form:
```bash
npm install @tanstack/react-form
```

**Create `src/components/PollCreationForm.tsx`**:
```typescript
import { useForm } from '@tanstack/react-form'
import { useCreatePoll } from '~/hooks/use-polls'
import { cn } from '~/lib/utils'

export function PollCreationForm() {
  const createPoll = useCreatePoll()

  const form = useForm({
    defaultValues: {
      question: '',
      options: ['', ''],
    },
    onSubmit: async ({ value }) => {
      // Filter out empty options
      const validOptions = value.options.filter(opt => opt.trim() !== '')

      if (validOptions.length < 2) {
        alert('Please provide at least 2 options')
        return
      }

      createPoll.mutate({
        question: value.question,
        options: validOptions,
      })
    },
  })

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        e.stopPropagation()
        form.handleSubmit()
      }}
      className="max-w-2xl mx-auto"
    >
      <div className="bg-white rounded-lg shadow-md p-6 space-y-6">
        {/* Question Input */}
        <form.Field
          name="question"
          validators={{
            onChange: ({ value }) =>
              !value ? 'Question is required' : undefined,
          }}
        >
          {(field) => (
            <div>
              <label htmlFor="question" className="block text-sm font-medium text-gray-700 mb-2">
                Poll Question
              </label>
              <input
                id="question"
                type="text"
                value={field.state.value}
                onBlur={field.handleBlur}
                onChange={(e) => field.handleChange(e.target.value)}
                placeholder="What is your favorite programming language?"
                className={cn(
                  "w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent",
                  field.state.meta.errors.length > 0 && "border-red-500"
                )}
              />
              {field.state.meta.errors.length > 0 && (
                <p className="mt-1 text-sm text-red-600">{field.state.meta.errors[0]}</p>
              )}
            </div>
          )}
        </form.Field>

        {/* Options */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Options
          </label>
          <form.Field name="options" mode="array">
            {(field) => (
              <div className="space-y-3">
                {field.state.value.map((_, i) => (
                  <form.Field key={i} name={`options[${i}]`}>
                    {(subField) => (
                      <div className="flex gap-2">
                        <input
                          type="text"
                          value={subField.state.value}
                          onChange={(e) => subField.handleChange(e.target.value)}
                          placeholder={`Option ${i + 1}`}
                          className="flex-1 px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                        {i >= 2 && (
                          <button
                            type="button"
                            onClick={() => {
                              const newOptions = [...field.state.value]
                              newOptions.splice(i, 1)
                              field.handleChange(newOptions)
                            }}
                            className="px-3 py-2 text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                          >
                            Remove
                          </button>
                        )}
                      </div>
                    )}
                  </form.Field>
                ))}
                <button
                  type="button"
                  onClick={() => field.handleChange([...field.state.value, ''])}
                  className="text-blue-600 hover:text-blue-700 font-medium text-sm"
                >
                  + Add Option
                </button>
              </div>
            )}
          </form.Field>
        </div>

        {/* Submit Button */}
        <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting]}>
          {([canSubmit, isSubmitting]) => (
            <button
              type="submit"
              disabled={!canSubmit || isSubmitting || createPoll.isPending}
              className={cn(
                "w-full px-6 py-3 font-semibold rounded-lg transition-colors",
                canSubmit && !isSubmitting && !createPoll.isPending
                  ? "bg-blue-600 text-white hover:bg-blue-700"
                  : "bg-gray-300 text-gray-500 cursor-not-allowed"
              )}
            >
              {createPoll.isPending ? 'Creating...' : 'Create Poll'}
            </button>
          )}
        </form.Subscribe>

        {/* Error Display */}
        {createPoll.isError && (
          <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
            <p className="text-red-800">
              {createPoll.error.message || 'Failed to create poll'}
            </p>
          </div>
        )}
      </div>
    </form>
  )
}
```

**Update `src/routes/create.tsx`**:
```typescript
import { createFileRoute } from '@tanstack/react-router'
import { PollCreationForm } from '~/components/PollCreationForm'

export const Route = createFileRoute('/create')({
  component: CreatePollPage,
})

function CreatePollPage() {
  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-6 text-center">
        Create New Poll
      </h1>
      <PollCreationForm />
    </div>
  )
}
```

**Verification**:
- Navigate to `/create`
- Fill out form and create poll
- Should navigate to `/poll/{pollId}` on success
- Error messages display for validation failures

---

### Step 3.3: Build Poll Voting and Results Page

**Create `src/components/PollResults.tsx`**:
```typescript
import { usePollResults, useAddVote } from '~/hooks/use-polls'
import { cn } from '~/lib/utils'

interface PollResultsProps {
  pollId: string
}

export function PollResults({ pollId }: PollResultsProps) {
  const { data: poll, isLoading, error } = usePollResults(pollId)
  const addVote = useAddVote(pollId)

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-6">
        <p className="text-red-800 font-medium">Failed to load poll</p>
        <p className="text-red-600 text-sm mt-1">{error.message}</p>
      </div>
    )
  }

  if (!poll) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6">
        <p className="text-yellow-800">Poll not found</p>
      </div>
    )
  }

  const handleVote = (optionIndex: number) => {
    addVote.mutate({ optionIndex })
  }

  return (
    <div className="max-w-3xl mx-auto">
      <div className="bg-white rounded-lg shadow-md p-6 space-y-6">
        {/* Poll Question */}
        <div>
          <h2 className="text-2xl font-bold text-gray-900 mb-2">{poll.question}</h2>
          <p className="text-sm text-gray-500">
            Total votes: {poll.totalVotes}
            {poll.voted && <span className="ml-2 text-green-600">✓ You voted</span>}
          </p>
        </div>

        {/* Options */}
        <div className="space-y-3">
          {poll.options.map((option, index) => {
            const percentage = poll.totalVotes > 0
              ? Math.round((option.votes / poll.totalVotes) * 100)
              : 0

            return (
              <button
                key={index}
                onClick={() => handleVote(index)}
                disabled={poll.voted || addVote.isPending}
                className={cn(
                  "w-full text-left p-4 rounded-lg border-2 transition-all",
                  poll.voted
                    ? "cursor-default"
                    : "hover:border-blue-500 hover:shadow-md cursor-pointer",
                  addVote.isPending && "opacity-50 cursor-wait"
                )}
              >
                <div className="flex justify-between items-center mb-2">
                  <span className="font-medium text-gray-900">{option.text}</span>
                  <span className="text-sm font-semibold text-gray-600">
                    {option.votes} votes ({percentage}%)
                  </span>
                </div>

                {/* Progress bar */}
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className="bg-blue-600 h-2 rounded-full transition-all duration-500"
                    style={{ width: `${percentage}%` }}
                  />
                </div>
              </button>
            )
          })}
        </div>

        {/* Error Display */}
        {addVote.isError && (
          <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
            <p className="text-red-800">
              {addVote.error.message || 'Failed to submit vote'}
            </p>
          </div>
        )}

        {/* Share Link */}
        <div className="pt-4 border-t border-gray-200">
          <p className="text-sm text-gray-600 mb-2">Share this poll:</p>
          <div className="flex gap-2">
            <input
              type="text"
              readOnly
              value={window.location.href}
              className="flex-1 px-3 py-2 bg-gray-50 border border-gray-300 rounded text-sm"
              onClick={(e) => e.currentTarget.select()}
            />
            <button
              onClick={() => {
                navigator.clipboard.writeText(window.location.href)
                alert('Link copied to clipboard!')
              }}
              className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700 transition-colors text-sm"
            >
              Copy
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
```

**Update `src/routes/poll/$pollId.tsx`**:
```typescript
import { createFileRoute } from '@tanstack/react-router'
import { PollResults } from '~/components/PollResults'

export const Route = createFileRoute('/poll/$pollId')({
  component: PollPage,
})

function PollPage() {
  const { pollId } = Route.useParams()

  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-6 text-center">Poll Results</h1>
      <PollResults pollId={pollId} />
    </div>
  )
}
```

**Verification**:
- Create a poll at `/create`
- View poll at `/poll/{pollId}`
- Vote on poll (should disable voting after first vote)
- Results update in real-time (polling every 5 seconds)
- Progress bars animate smoothly

---

## Phase 4: Real-Time Updates with SignalR (Optional)

### Step 4.1: Install SignalR Client

If the API implements SignalR hub for real-time updates (as described in Phase 8 Step 8.3):

```bash
npm install @microsoft/signalr
```

---

### Step 4.2: Create SignalR Hook

**Create `src/hooks/use-poll-signalr.ts`**:
```typescript
import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import * as signalR from '@microsoft/signalr'
import { pollKeys } from './use-polls'
import type { PollResultsResponse } from '~/lib/api-types'

const SIGNALR_HUB_URL = import.meta.env.VITE_API_URL + '/pollHub'

export function usePollSignalR(pollId: string) {
  const queryClient = useQueryClient()

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_HUB_URL)
      .withAutomaticReconnect()
      .build()

    connection.on('PollUpdated', (updatedPollId: string, results: PollResultsResponse) => {
      if (updatedPollId === pollId) {
        // Update cache with real-time results
        queryClient.setQueryData(pollKeys.detail(pollId), results)
      }
    })

    connection.start()
      .then(() => {
        // Subscribe to poll updates
        connection.invoke('WatchPoll', pollId)
      })
      .catch(err => console.error('SignalR connection error:', err))

    return () => {
      connection.invoke('UnwatchPoll', pollId).catch(() => {})
      connection.stop()
    }
  }, [pollId, queryClient])
}
```

---

### Step 4.3: Update PollResults to Use SignalR

**Update `src/components/PollResults.tsx`**:
```typescript
import { usePollResults, useAddVote } from '~/hooks/use-polls'
import { usePollSignalR } from '~/hooks/use-poll-signalr'
import { cn } from '~/lib/utils'

interface PollResultsProps {
  pollId: string
}

export function PollResults({ pollId }: PollResultsProps) {
  const { data: poll, isLoading, error } = usePollResults(pollId)
  const addVote = useAddVote(pollId)

  // Enable real-time updates via SignalR
  usePollSignalR(pollId)

  // ... rest of component unchanged
}
```

**Update `src/hooks/use-polls.ts`** - Remove polling interval:
```typescript
export function usePollResults(pollId: string) {
  return useQuery({
    queryKey: pollKeys.detail(pollId),
    queryFn: () => apiClient.getPollResults(pollId),
    // Remove refetchInterval - SignalR provides real-time updates
  })
}
```

**Verification**:
- Open poll in two browser windows
- Vote in one window
- Other window updates instantly via SignalR

---

## Phase 5: Advanced Features (Optional)

### Step 5.1: Add Loading Skeletons

**Create `src/components/PollSkeleton.tsx`**:
```typescript
export function PollSkeleton() {
  return (
    <div className="max-w-3xl mx-auto">
      <div className="bg-white rounded-lg shadow-md p-6 space-y-6 animate-pulse">
        <div className="h-8 bg-gray-200 rounded w-3/4"></div>
        <div className="h-4 bg-gray-200 rounded w-1/4"></div>
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="p-4 rounded-lg border-2 border-gray-200">
              <div className="h-6 bg-gray-200 rounded w-1/2 mb-2"></div>
              <div className="h-2 bg-gray-200 rounded"></div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
```

**Update `src/components/PollResults.tsx`**:
```typescript
if (isLoading) {
  return <PollSkeleton />
}
```

---

### Step 5.2: Add Recent Polls List (Optional)

If API supports listing recent polls:

**Create `src/routes/polls/index.tsx`**:
```typescript
import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '~/lib/api-client'

export const Route = createFileRoute('/polls/')({
  component: PollsListPage,
})

function PollsListPage() {
  const { data: polls, isLoading } = useQuery({
    queryKey: ['polls', 'recent'],
    queryFn: () => apiClient.getRecentPolls(), // Assumes API endpoint exists
  })

  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-6">Recent Polls</h1>
      {isLoading ? (
        <p>Loading...</p>
      ) : (
        <div className="grid gap-4">
          {polls?.map(poll => (
            <Link
              key={poll.pollId}
              to="/poll/$pollId"
              params={{ pollId: poll.pollId }}
              className="block p-6 bg-white rounded-lg shadow hover:shadow-md transition-shadow"
            >
              <h3 className="font-semibold text-lg">{poll.question}</h3>
              <p className="text-gray-600 text-sm mt-1">{poll.totalVotes} votes</p>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
```

---

### Step 5.3: Add TanStack Table for Admin Dashboard (Optional)

If building an admin view with large datasets:

```bash
npm install @tanstack/react-table
```

**Create `src/routes/admin/polls.tsx`**:
```typescript
import { createFileRoute } from '@tanstack/react-router'
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  createColumnHelper,
  flexRender,
} from '@tanstack/react-table'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '~/lib/api-client'

export const Route = createFileRoute('/admin/polls')({
  component: AdminPollsPage,
})

const columnHelper = createColumnHelper<PollSummary>()

const columns = [
  columnHelper.accessor('pollId', {
    header: 'Poll ID',
    cell: info => info.getValue(),
  }),
  columnHelper.accessor('question', {
    header: 'Question',
    cell: info => info.getValue(),
  }),
  columnHelper.accessor('totalVotes', {
    header: 'Total Votes',
    cell: info => info.getValue(),
  }),
  columnHelper.accessor('createdAt', {
    header: 'Created',
    cell: info => new Date(info.getValue()).toLocaleDateString(),
  }),
]

function AdminPollsPage() {
  const { data: polls = [] } = useQuery({
    queryKey: ['admin', 'polls'],
    queryFn: () => apiClient.getAllPolls(),
  })

  const table = useReactTable({
    data: polls,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  return (
    <div>
      <h1 className="text-3xl font-bold mb-6">Poll Administration</h1>
      <div className="bg-white rounded-lg shadow overflow-hidden">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            {table.getHeaderGroups().map(headerGroup => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map(header => (
                  <th
                    key={header.id}
                    className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                    onClick={header.column.getToggleSortingHandler()}
                  >
                    {flexRender(header.column.columnDef.header, header.getContext())}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {table.getRowModel().rows.map(row => (
              <tr key={row.id} className="hover:bg-gray-50">
                {row.getVisibleCells().map(cell => (
                  <td key={cell.id} className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
```

---

### Step 5.4: Add TanStack Virtual for Large Option Lists (Optional)

If polls can have hundreds of options:

```bash
npm install @tanstack/react-virtual
```

**Example usage in `PollResults.tsx`**:
```typescript
import { useVirtualizer } from '@tanstack/react-virtual'
import { useRef } from 'react'

// Inside component:
const parentRef = useRef<HTMLDivElement>(null)

const virtualizer = useVirtualizer({
  count: poll.options.length,
  getScrollElement: () => parentRef.current,
  estimateSize: () => 80, // Estimated height per option
})

// Render:
<div ref={parentRef} className="h-96 overflow-auto">
  <div
    style={{
      height: `${virtualizer.getTotalSize()}px`,
      width: '100%',
      position: 'relative',
    }}
  >
    {virtualizer.getVirtualItems().map(virtualRow => {
      const option = poll.options[virtualRow.index]
      return (
        <div
          key={virtualRow.index}
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            width: '100%',
            height: `${virtualRow.size}px`,
            transform: `translateY(${virtualRow.start}px)`,
          }}
        >
          {/* Option content */}
        </div>
      )
    })}
  </div>
</div>
```

---

## Phase 6: Testing Strategy

### Step 6.1: Setup Testing Infrastructure

```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

**Create `vitest.config.ts`**:
```typescript
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
  resolve: {
    alias: {
      '~': path.resolve(__dirname, './src'),
    },
  },
})
```

**Create `src/test/setup.ts`**:
```typescript
import { expect, afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'
import * as matchers from '@testing-library/jest-dom/matchers'

expect.extend(matchers)

afterEach(() => {
  cleanup()
})
```

---

### Step 6.2: Component Tests

**Create `src/components/__tests__/PollResults.test.tsx`**:
```typescript
import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { PollResults } from '../PollResults'
import * as usePollsHooks from '~/hooks/use-polls'

// Mock hooks
vi.mock('~/hooks/use-polls')

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
}

describe('PollResults', () => {
  it('displays loading state initially', () => {
    vi.mocked(usePollsHooks.usePollResults).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as any)

    vi.mocked(usePollsHooks.useAddVote).mockReturnValue({} as any)

    render(<PollResults pollId="test-123" />, { wrapper: createWrapper() })

    expect(screen.getByRole('status')).toBeInTheDocument() // Loading spinner
  })

  it('displays poll question and options', async () => {
    const mockPoll = {
      pollId: 'test-123',
      question: 'Favorite color?',
      options: [
        { text: 'Red', votes: 5 },
        { text: 'Blue', votes: 3 },
      ],
      voted: false,
      totalVotes: 8,
    }

    vi.mocked(usePollsHooks.usePollResults).mockReturnValue({
      data: mockPoll,
      isLoading: false,
      error: null,
    } as any)

    vi.mocked(usePollsHooks.useAddVote).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    } as any)

    render(<PollResults pollId="test-123" />, { wrapper: createWrapper() })

    expect(screen.getByText('Favorite color?')).toBeInTheDocument()
    expect(screen.getByText('Red')).toBeInTheDocument()
    expect(screen.getByText('Blue')).toBeInTheDocument()
    expect(screen.getByText(/5 votes.*63%/)).toBeInTheDocument()
  })

  it('submits vote when option clicked', async () => {
    const user = userEvent.setup()
    const mockMutate = vi.fn()

    vi.mocked(usePollsHooks.usePollResults).mockReturnValue({
      data: {
        pollId: 'test-123',
        question: 'Test?',
        options: [{ text: 'Option 1', votes: 0 }],
        voted: false,
        totalVotes: 0,
      },
      isLoading: false,
      error: null,
    } as any)

    vi.mocked(usePollsHooks.useAddVote).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
      isError: false,
    } as any)

    render(<PollResults pollId="test-123" />, { wrapper: createWrapper() })

    await user.click(screen.getByText('Option 1'))

    expect(mockMutate).toHaveBeenCalledWith({ optionIndex: 0 })
  })
})
```

**Add test script to `package.json`**:
```json
{
  "scripts": {
    "test": "vitest",
    "test:ui": "vitest --ui"
  }
}
```

**Verification**: Run `npm test` - tests pass

---

### Step 6.3: E2E Tests with Playwright (Optional)

```bash
npm install -D @playwright/test
npx playwright install
```

**Create `e2e/poll-flow.spec.ts`**:
```typescript
import { test, expect } from '@playwright/test'

test('complete poll creation and voting flow', async ({ page, context }) => {
  // Navigate to home
  await page.goto('http://localhost:5173')

  // Create poll
  await page.click('text=Create Your First Poll')
  await page.fill('input[placeholder*="programming language"]', 'Favorite framework?')
  await page.fill('input[placeholder="Option 1"]', 'React')
  await page.fill('input[placeholder="Option 2"]', 'Vue')
  await page.click('button:has-text("Create Poll")')

  // Should navigate to poll page
  await expect(page).toHaveURL(/\/poll\/[a-zA-Z0-9]+/)
  await expect(page.locator('text=Favorite framework?')).toBeVisible()

  // Vote
  await page.click('button:has-text("React")')
  await expect(page.locator('text=✓ You voted')).toBeVisible()

  // Open in second browser context to verify real-time updates
  const page2 = await context.newPage()
  await page2.goto(page.url())
  await page2.click('button:has-text("Vue")')

  // First page should see updated vote count
  await expect(page.locator('text=Total votes: 2')).toBeVisible()
})
```

---

## Phase 7: Build and Deployment

### Step 7.1: Environment Configuration

**Create `.env.production`**:
```
VITE_API_URL=https://api.yourapp.com
```

**Create `.env.development`**:
```
VITE_API_URL=http://localhost:5000
```

---

### Step 7.2: Build Optimization

**Update `vite.config.ts`** - Add build optimizations:
```typescript
export default defineConfig({
  plugins: [TanStackRouterVite(), react()],
  resolve: {
    alias: { '~': path.resolve(__dirname, './src') },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor-react': ['react', 'react-dom'],
          'vendor-tanstack': [
            '@tanstack/react-query',
            '@tanstack/react-router',
            '@tanstack/react-form',
          ],
        },
      },
    },
  },
})
```

---

### Step 7.3: Build and Preview

```bash
# Build for production
npm run build

# Preview production build
npm run preview
```

**Verification**:
- Build completes without errors
- Preview server runs at http://localhost:4173
- All features work in production build

---

### Step 7.4: Integration with .NET Aspire AppHost

**Update `OrleansVoting.AppHost/Program.cs`** - Add React frontend:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Existing services
var redis = builder.AddRedis("voting-redis");

var silo = builder.AddProject<Projects.OrleansVoting_Silo>("silo")
    .WithReference(redis)
    .WithReplicas(3);

var api = builder.AddProject<Projects.OrleansVoting_Api>("api")
    .WithReference(redis);

// Add React frontend (serves static files from dist/)
var frontend = builder.AddNpmApp("frontend", "../OrleansVoting.Web")
    .WithReference(api)
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"))
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .PublishAsDockerFile();

builder.Build().Run();
```

**Verification**: `dotnet run --project OrleansVoting.AppHost` starts all services including React frontend

---

## Implementation Checklist

### Phase 1: Foundation
- [ ] Initialize Vite + React + TypeScript project
- [ ] Install TanStack Router, Query, and Tailwind
- [ ] Configure TypeScript and Vite with path aliases
- [ ] Create API client with type definitions
- [ ] Setup environment variables

### Phase 2: Routing
- [ ] Define route structure (/, /create, /poll/$pollId)
- [ ] Create root layout with header and navigation
- [ ] Setup router and query client in main.tsx
- [ ] Generate route types with TanStack Router CLI
- [ ] Verify routing works with devtools

### Phase 3: Data Management
- [ ] Create query hooks (usePollResults, useCreatePoll, useAddVote)
- [ ] Install and configure TanStack Form
- [ ] Build poll creation form with validation
- [ ] Build poll results component with voting
- [ ] Test query caching and mutations

### Phase 4: Real-Time (Optional)
- [ ] Install SignalR client
- [ ] Create SignalR hook for poll updates
- [ ] Update components to use SignalR
- [ ] Test real-time updates across multiple windows

### Phase 5: Advanced Features (Optional)
- [ ] Add loading skeletons
- [ ] Add recent polls list (if API supports)
- [ ] Add TanStack Table for admin dashboard
- [ ] Add TanStack Virtual for large lists

### Phase 6: Testing
- [ ] Setup Vitest and Testing Library
- [ ] Write component tests
- [ ] Write integration tests
- [ ] Setup Playwright for E2E (optional)

### Phase 7: Deployment
- [ ] Configure environment variables
- [ ] Optimize build configuration
- [ ] Test production build
- [ ] Integrate with Aspire AppHost
- [ ] Deploy to hosting service

---

## Additional TanStack Libraries to Consider

### TanStack Form
**Status**: Included in Phase 3
**Use Case**: Type-safe form validation and state management
**Benefits**: Better than react-hook-form for complex forms with TypeScript

### TanStack Table
**Status**: Optional (Phase 5)
**Use Case**: Admin dashboard with sortable, filterable poll lists
**Benefits**: Headless table with full control over UI, excellent performance

### TanStack Virtual
**Status**: Optional (Phase 5)
**Use Case**: Polls with hundreds of options
**Benefits**: Render only visible items, massive performance improvement

### TanStack Store (Future)
**Status**: Not included
**Use Case**: Client-side state management if needed
**Benefits**: Alternative to Zustand/Redux, integrates well with TanStack ecosystem

---

## Development Tips

1. **Use Devtools**: Keep TanStack Router and Query devtools open during development
2. **Type Safety**: Let TypeScript guide you - don't use `any` types
3. **Query Keys**: Use consistent query key factory pattern (`pollKeys`)
4. **Optimistic Updates**: Use `onSuccess` in mutations to update cache immediately
5. **Error Boundaries**: Add React error boundaries for production
6. **Accessibility**: Use semantic HTML and ARIA labels
7. **Performance**: Use React DevTools Profiler to identify slow renders
8. **Code Splitting**: Route-based code splitting is automatic with TanStack Router

---

## Resources

- [TanStack Router Docs](https://tanstack.com/router/latest)
- [TanStack Query Docs](https://tanstack.com/query/latest)
- [TanStack Form Docs](https://tanstack.com/form/latest)
- [TanStack Table Docs](https://tanstack.com/table/latest)
- [Tailwind CSS Docs](https://tailwindcss.com/docs)
- [Vite Docs](https://vitejs.dev)

---

## Next Steps After Completion

Once the React frontend is complete, consider:

1. **Mobile App**: Use React Native with same TanStack hooks
2. **PWA**: Add service worker for offline support
3. **Analytics**: Add Plausible or PostHog for usage tracking
4. **Monitoring**: Add Sentry for error tracking
5. **Performance**: Add Lighthouse CI to PR checks
6. **Internationalization**: Add i18n support for multiple languages

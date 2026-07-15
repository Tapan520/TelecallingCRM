# TelecallingCRM

A lightweight, web-based CRM tool built for Telecalling businesses. It helps teams manage leads, log calls, and track follow-ups — all from a clean browser interface.

## Features

- **Dashboard** – at-a-glance summary of total leads, calls made today, pending follow-ups, and converted leads
- **Lead Management** – add, edit, search, filter, and delete contacts (leads)
- **Call Logging** – record every call with outcome, duration, and notes
- **Follow-up Scheduling** – schedule future callbacks and mark them complete
- **Status Tracking** – track each lead through New → In Progress → Interested/Converted
- **REST API** – `/api/stats` endpoint for aggregated statistics

## Tech Stack

| Layer    | Technology                |
|----------|---------------------------|
| Backend  | Python 3 / Flask          |
| Database | SQLite (via SQLAlchemy)    |
| Frontend | Bootstrap 5 + Bootstrap Icons |

## Setup

```bash
# 1. Clone the repository
git clone https://github.com/Tapan520/TelecallingCRM.git
cd TelecallingCRM

# 2. Create and activate a virtual environment (recommended)
python -m venv venv
source venv/bin/activate   # Windows: venv\Scripts\activate

# 3. Install dependencies
pip install -r requirements.txt

# 4. Run the app
python app.py
```

Open <http://127.0.0.1:5000> in your browser.

## Configuration

| Environment Variable | Default                      | Description               |
|----------------------|------------------------------|---------------------------|
| `SECRET_KEY`         | `telecalling-crm-secret-key` | Flask session secret      |
| `DATABASE_URL`       | `sqlite:///telecalling_crm.db` | SQLAlchemy database URI |

## Running Tests

```bash
pip install pytest
python -m pytest tests.py -v
```

## Lead Statuses

| Status         | Meaning                           |
|----------------|-----------------------------------|
| New            | Freshly added, not yet contacted  |
| In Progress    | Actively being worked             |
| Interested     | Showed interest                   |
| Not Interested | Declined                          |
| Converted      | Successfully closed               |
| Do Not Call    | Opted out                         |

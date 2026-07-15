"""
Tests for the Telecalling CRM application.
"""

import pytest
from datetime import datetime, timedelta, timezone
from app import app, db, Lead, CallLog, FollowUp


@pytest.fixture
def client():
    """Create a test client with an in-memory database."""
    app.config['TESTING'] = True
    app.config['SQLALCHEMY_DATABASE_URI'] = 'sqlite:///:memory:'
    app.config['WTF_CSRF_ENABLED'] = False
    with app.test_client() as client:
        with app.app_context():
            db.create_all()
        yield client
        with app.app_context():
            db.drop_all()


@pytest.fixture
def sample_lead(client):
    """Create a sample lead in the database."""
    with app.app_context():
        lead = Lead(name='Alice Smith', phone='9876543210', email='alice@example.com',
                    company='Acme Corp', source='Referral', status='New')
        db.session.add(lead)
        db.session.commit()
        return lead.id


# ---------------------------------------------------------------------------
# Dashboard
# ---------------------------------------------------------------------------

class TestDashboard:
    def test_dashboard_loads(self, client):
        response = client.get('/')
        assert response.status_code == 200
        assert b'Dashboard' in response.data

    def test_dashboard_shows_stats(self, client, sample_lead):
        response = client.get('/')
        assert response.status_code == 200
        assert b'Total Leads' in response.data


# ---------------------------------------------------------------------------
# Leads
# ---------------------------------------------------------------------------

class TestLeads:
    def test_leads_list_empty(self, client):
        response = client.get('/leads')
        assert response.status_code == 200
        assert b'No leads found' in response.data

    def test_leads_list_with_lead(self, client, sample_lead):
        response = client.get('/leads')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data

    def test_new_lead_form(self, client):
        response = client.get('/leads/new')
        assert response.status_code == 200
        assert b'New Lead' in response.data

    def test_create_lead(self, client):
        response = client.post('/leads/new', data={
            'name': 'Bob Jones',
            'phone': '1234567890',
            'email': 'bob@example.com',
            'company': 'BobCo',
            'source': 'Website',
            'status': 'New',
            'notes': 'Test lead',
        }, follow_redirects=True)
        assert response.status_code == 200
        assert b'Bob Jones' in response.data
        assert b'added successfully' in response.data

    def test_create_lead_missing_name(self, client):
        response = client.post('/leads/new', data={
            'name': '',
            'phone': '1234567890',
        })
        # Server-side validation: form should re-render with 200 and show an error
        assert response.status_code == 200
        assert b'required' in response.data.lower()

    def test_lead_detail(self, client, sample_lead):
        response = client.get(f'/leads/{sample_lead}')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data

    def test_lead_detail_not_found(self, client):
        response = client.get('/leads/9999')
        assert response.status_code == 404

    def test_edit_lead(self, client, sample_lead):
        response = client.post(f'/leads/{sample_lead}/edit', data={
            'name': 'Alice Updated',
            'phone': '9876543210',
            'email': 'alice@example.com',
            'company': 'NewCorp',
            'source': 'Referral',
            'status': 'Interested',
            'notes': '',
        }, follow_redirects=True)
        assert response.status_code == 200
        assert b'Alice Updated' in response.data
        assert b'updated successfully' in response.data

    def test_delete_lead(self, client, sample_lead):
        response = client.post(f'/leads/{sample_lead}/delete', follow_redirects=True)
        assert response.status_code == 200
        assert b'deleted' in response.data

    def test_leads_search(self, client, sample_lead):
        response = client.get('/leads?search=Alice')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data

    def test_leads_search_no_match(self, client, sample_lead):
        response = client.get('/leads?search=ZZZNOTFOUND')
        assert response.status_code == 200
        assert b'Alice Smith' not in response.data

    def test_leads_filter_by_status(self, client, sample_lead):
        response = client.get('/leads?status=New')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data

    def test_leads_filter_by_different_status(self, client, sample_lead):
        response = client.get('/leads?status=Converted')
        assert response.status_code == 200
        assert b'Alice Smith' not in response.data


# ---------------------------------------------------------------------------
# Call Logs
# ---------------------------------------------------------------------------

class TestCallLogs:
    def test_calls_list_empty(self, client):
        response = client.get('/calls')
        assert response.status_code == 200
        assert b'No calls' in response.data

    def test_log_call(self, client, sample_lead):
        response = client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Answered',
            'duration_minutes': '5',
            'notes': 'Discussed pricing',
        }, follow_redirects=True)
        assert response.status_code == 200
        assert b'Call logged successfully' in response.data

    def test_log_call_updates_lead_status(self, client, sample_lead):
        client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Answered',
            'duration_minutes': '',
            'notes': '',
        }, follow_redirects=True)
        with app.app_context():
            lead = db.session.get(Lead, sample_lead)
            assert lead.status == 'In Progress'

    def test_log_call_callback_requested(self, client, sample_lead):
        client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Callback Requested',
            'duration_minutes': '3',
            'notes': 'Will call back tomorrow',
        }, follow_redirects=True)
        with app.app_context():
            lead = db.session.get(Lead, sample_lead)
            assert lead.status == 'In Progress'

    def test_calls_list_shows_logged_call(self, client, sample_lead):
        client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Answered',
            'duration_minutes': '2',
            'notes': '',
        })
        response = client.get('/calls')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data

    def test_delete_call_log(self, client, sample_lead):
        client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Answered',
            'duration_minutes': '',
            'notes': '',
        })
        with app.app_context():
            call = CallLog.query.filter_by(lead_id=sample_lead).first()
            call_id = call.id

        response = client.post(f'/calls/{call_id}/delete', follow_redirects=True)
        assert response.status_code == 200
        assert b'deleted' in response.data


# ---------------------------------------------------------------------------
# Follow-ups
# ---------------------------------------------------------------------------

class TestFollowUps:
    def test_follow_ups_list_empty(self, client):
        response = client.get('/follow-ups')
        assert response.status_code == 200
        assert b'No pending follow-ups' in response.data

    def test_schedule_follow_up(self, client, sample_lead):
        future = datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=1)
        response = client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': future.strftime('%Y-%m-%dT%H:%M'),
            'reason': 'Check interest',
        }, follow_redirects=True)
        assert response.status_code == 200
        assert b'Follow-up scheduled successfully' in response.data

    def test_schedule_follow_up_invalid_date(self, client, sample_lead):
        response = client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': 'not-a-date',
            'reason': 'Check interest',
        }, follow_redirects=True)
        assert response.status_code == 200
        assert b'Invalid date' in response.data

    def test_complete_follow_up(self, client, sample_lead):
        future = datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=1)
        client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': future.strftime('%Y-%m-%dT%H:%M'),
            'reason': 'Test',
        })
        with app.app_context():
            fu = FollowUp.query.filter_by(lead_id=sample_lead).first()
            fu_id = fu.id

        response = client.post(f'/follow-ups/{fu_id}/complete', follow_redirects=True)
        assert response.status_code == 200
        assert b'marked as completed' in response.data

        with app.app_context():
            fu = db.session.get(FollowUp, fu_id)
            assert fu.completed is True
            assert fu.completed_at is not None

    def test_delete_follow_up(self, client, sample_lead):
        future = datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=1)
        client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': future.strftime('%Y-%m-%dT%H:%M'),
            'reason': 'Delete me',
        })
        with app.app_context():
            fu = FollowUp.query.filter_by(lead_id=sample_lead).first()
            fu_id = fu.id

        response = client.post(f'/follow-ups/{fu_id}/delete', follow_redirects=True)
        assert response.status_code == 200
        assert b'deleted' in response.data

    def test_follow_ups_list_shows_pending(self, client, sample_lead):
        future = datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=1)
        client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': future.strftime('%Y-%m-%dT%H:%M'),
            'reason': 'Pending test',
        })
        response = client.get('/follow-ups')
        assert response.status_code == 200
        assert b'Alice Smith' in response.data


# ---------------------------------------------------------------------------
# API stats
# ---------------------------------------------------------------------------

class TestAPI:
    def test_api_stats(self, client):
        import json
        response = client.get('/api/stats')
        assert response.status_code == 200
        data = json.loads(response.data)
        assert 'total_leads' in data
        assert 'calls_today' in data
        assert 'pending_follow_ups' in data
        assert 'converted_leads' in data
        assert 'leads_by_status' in data
        assert 'calls_by_outcome' in data

    def test_api_stats_counts_correctly(self, client, sample_lead):
        import json
        response = client.get('/api/stats')
        data = json.loads(response.data)
        assert data['total_leads'] == 1
        assert data['leads_by_status']['New'] == 1


# ---------------------------------------------------------------------------
# Cascade delete
# ---------------------------------------------------------------------------

class TestCascadeDelete:
    def test_deleting_lead_removes_call_logs_and_follow_ups(self, client, sample_lead):
        # Create a call log and a follow-up
        client.post(f'/leads/{sample_lead}/calls/new', data={
            'outcome': 'Answered', 'duration_minutes': '2', 'notes': '',
        })
        future = datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=1)
        client.post(f'/leads/{sample_lead}/follow-ups/new', data={
            'scheduled_at': future.strftime('%Y-%m-%dT%H:%M'), 'reason': 'Test',
        })

        with app.app_context():
            assert CallLog.query.filter_by(lead_id=sample_lead).count() == 1
            assert FollowUp.query.filter_by(lead_id=sample_lead).count() == 1

        client.post(f'/leads/{sample_lead}/delete')

        with app.app_context():
            assert db.session.get(Lead, sample_lead) is None
            assert CallLog.query.filter_by(lead_id=sample_lead).count() == 0
            assert FollowUp.query.filter_by(lead_id=sample_lead).count() == 0

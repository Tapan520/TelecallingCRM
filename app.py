"""
Telecalling CRM - Main Application
A CRM tool for managing telecalling business operations.
"""

from flask import Flask, render_template, request, redirect, url_for, flash, jsonify
from flask_sqlalchemy import SQLAlchemy
from datetime import datetime, date, timezone
import os

app = Flask(__name__)
app.config['SECRET_KEY'] = os.environ.get('SECRET_KEY', 'telecalling-crm-secret-key')
app.config['SQLALCHEMY_DATABASE_URI'] = os.environ.get(
    'DATABASE_URL', 'sqlite:///telecalling_crm.db'
)
app.config['SQLALCHEMY_TRACK_MODIFICATIONS'] = False

db = SQLAlchemy(app)


# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def _now() -> datetime:
    """Return the current UTC time as a naive datetime (for SQLite compatibility)."""
    return datetime.now(timezone.utc).replace(tzinfo=None)


# ---------------------------------------------------------------------------
# Template context helpers
# ---------------------------------------------------------------------------

_STATUS_BADGE = {
    'New': 'bg-secondary',
    'In Progress': 'bg-primary',
    'Interested': 'bg-info text-dark',
    'Not Interested': 'bg-danger',
    'Converted': 'bg-success',
    'Do Not Call': 'bg-dark',
}

_OUTCOME_BADGE = {
    'Answered': 'bg-success',
    'No Answer': 'bg-secondary',
    'Busy': 'bg-warning text-dark',
    'Voicemail': 'bg-info text-dark',
    'Callback Requested': 'bg-primary',
    'Wrong Number': 'bg-danger',
}


@app.context_processor
def inject_helpers():
    def status_badge(status):
        return _STATUS_BADGE.get(status, 'bg-secondary')

    def outcome_badge(outcome):
        return _OUTCOME_BADGE.get(outcome, 'bg-secondary')

    return dict(status_badge=status_badge, outcome_badge=outcome_badge, now=_now())


# ---------------------------------------------------------------------------
# Models
# ---------------------------------------------------------------------------

class Lead(db.Model):
    """Represents a potential or existing customer (lead/contact)."""
    __tablename__ = 'leads'

    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(120), nullable=False)
    phone = db.Column(db.String(30), nullable=False)
    email = db.Column(db.String(120), nullable=True)
    company = db.Column(db.String(120), nullable=True)
    source = db.Column(db.String(80), nullable=True)
    status = db.Column(
        db.String(30),
        nullable=False,
        default='New'
    )
    notes = db.Column(db.Text, nullable=True)
    created_at = db.Column(db.DateTime, default=_now)
    updated_at = db.Column(db.DateTime, default=_now, onupdate=_now)

    call_logs = db.relationship('CallLog', backref='lead', lazy=True, cascade='all, delete-orphan')
    follow_ups = db.relationship('FollowUp', backref='lead', lazy=True, cascade='all, delete-orphan')

    STATUSES = ['New', 'In Progress', 'Interested', 'Not Interested', 'Converted', 'Do Not Call']
    SOURCES = ['Website', 'Referral', 'Cold Call', 'Social Media', 'Advertisement', 'Other']

    def __repr__(self):
        return f'<Lead {self.name}>'

    @property
    def last_call(self):
        return (
            CallLog.query
            .filter_by(lead_id=self.id)
            .order_by(CallLog.called_at.desc())
            .first()
        )

    @property
    def next_follow_up(self):
        return (
            FollowUp.query
            .filter_by(lead_id=self.id, completed=False)
            .filter(FollowUp.scheduled_at >= _now())
            .order_by(FollowUp.scheduled_at.asc())
            .first()
        )


class CallLog(db.Model):
    """Represents a single call made to a lead."""
    __tablename__ = 'call_logs'

    id = db.Column(db.Integer, primary_key=True)
    lead_id = db.Column(db.Integer, db.ForeignKey('leads.id'), nullable=False)
    called_at = db.Column(db.DateTime, nullable=False, default=_now)
    duration_minutes = db.Column(db.Integer, nullable=True)
    outcome = db.Column(db.String(40), nullable=False, default='Answered')
    notes = db.Column(db.Text, nullable=True)
    created_at = db.Column(db.DateTime, default=_now)

    OUTCOMES = ['Answered', 'No Answer', 'Busy', 'Voicemail', 'Callback Requested', 'Wrong Number']

    def __repr__(self):
        return f'<CallLog {self.id} lead={self.lead_id} outcome={self.outcome}>'


class FollowUp(db.Model):
    """Represents a scheduled follow-up call."""
    __tablename__ = 'follow_ups'

    id = db.Column(db.Integer, primary_key=True)
    lead_id = db.Column(db.Integer, db.ForeignKey('leads.id'), nullable=False)
    scheduled_at = db.Column(db.DateTime, nullable=False)
    reason = db.Column(db.String(200), nullable=True)
    completed = db.Column(db.Boolean, default=False)
    completed_at = db.Column(db.DateTime, nullable=True)
    created_at = db.Column(db.DateTime, default=_now)

    def __repr__(self):
        return f'<FollowUp {self.id} lead={self.lead_id} scheduled={self.scheduled_at}>'


# ---------------------------------------------------------------------------
# Routes – Dashboard
# ---------------------------------------------------------------------------

@app.route('/')
def dashboard():
    today_start = datetime.combine(date.today(), datetime.min.time())
    today_end = datetime.combine(date.today(), datetime.max.time())

    total_leads = Lead.query.count()
    calls_today = CallLog.query.filter(
        CallLog.called_at >= today_start,
        CallLog.called_at <= today_end
    ).count()
    pending_follow_ups = FollowUp.query.filter_by(completed=False).count()
    converted_leads = Lead.query.filter_by(status='Converted').count()

    recent_calls = (
        CallLog.query
        .order_by(CallLog.called_at.desc())
        .limit(10)
        .all()
    )
    upcoming_follow_ups = (
        FollowUp.query
        .filter_by(completed=False)
        .filter(FollowUp.scheduled_at >= _now())
        .order_by(FollowUp.scheduled_at.asc())
        .limit(10)
        .all()
    )

    return render_template(
        'dashboard.html',
        total_leads=total_leads,
        calls_today=calls_today,
        pending_follow_ups=pending_follow_ups,
        converted_leads=converted_leads,
        recent_calls=recent_calls,
        upcoming_follow_ups=upcoming_follow_ups,
    )


# ---------------------------------------------------------------------------
# Routes – Leads
# ---------------------------------------------------------------------------

@app.route('/leads')
def leads_list():
    status_filter = request.args.get('status', '')
    search = request.args.get('search', '').strip()

    query = Lead.query
    if status_filter:
        query = query.filter_by(status=status_filter)
    if search:
        like = f'%{search}%'
        query = query.filter(
            db.or_(
                Lead.name.ilike(like),
                Lead.phone.ilike(like),
                Lead.company.ilike(like),
                Lead.email.ilike(like),
            )
        )
    leads = query.order_by(Lead.created_at.desc()).all()
    return render_template(
        'leads_list.html',
        leads=leads,
        statuses=Lead.STATUSES,
        status_filter=status_filter,
        search=search,
    )


@app.route('/leads/new', methods=['GET', 'POST'])
def lead_new():
    if request.method == 'POST':
        name = request.form.get('name', '').strip()
        phone = request.form.get('phone', '').strip()
        if not name or not phone:
            flash('Name and Phone are required.', 'danger')
            return render_template('lead_form.html', lead=None, statuses=Lead.STATUSES, sources=Lead.SOURCES)
        lead = Lead(
            name=name,
            phone=phone,
            email=request.form.get('email', '').strip() or None,
            company=request.form.get('company', '').strip() or None,
            source=request.form.get('source', '').strip() or None,
            status=request.form.get('status', 'New'),
            notes=request.form.get('notes', '').strip() or None,
        )
        db.session.add(lead)
        db.session.commit()
        flash(f'Lead "{lead.name}" added successfully.', 'success')
        return redirect(url_for('lead_detail', lead_id=lead.id))
    return render_template('lead_form.html', lead=None, statuses=Lead.STATUSES, sources=Lead.SOURCES)


@app.route('/leads/<int:lead_id>')
def lead_detail(lead_id):
    lead = db.get_or_404(Lead, lead_id)
    call_logs = (
        CallLog.query
        .filter_by(lead_id=lead_id)
        .order_by(CallLog.called_at.desc())
        .all()
    )
    follow_ups = (
        FollowUp.query
        .filter_by(lead_id=lead_id)
        .order_by(FollowUp.scheduled_at.desc())
        .all()
    )
    return render_template(
        'lead_detail.html',
        lead=lead,
        call_logs=call_logs,
        follow_ups=follow_ups,
        outcomes=CallLog.OUTCOMES,
    )


@app.route('/leads/<int:lead_id>/edit', methods=['GET', 'POST'])
def lead_edit(lead_id):
    lead = db.get_or_404(Lead, lead_id)
    if request.method == 'POST':
        lead.name = request.form['name'].strip()
        lead.phone = request.form['phone'].strip()
        lead.email = request.form.get('email', '').strip() or None
        lead.company = request.form.get('company', '').strip() or None
        lead.source = request.form.get('source', '').strip() or None
        lead.status = request.form.get('status', lead.status)
        lead.notes = request.form.get('notes', '').strip() or None
        lead.updated_at = _now()
        db.session.commit()
        flash(f'Lead "{lead.name}" updated successfully.', 'success')
        return redirect(url_for('lead_detail', lead_id=lead.id))
    return render_template('lead_form.html', lead=lead, statuses=Lead.STATUSES, sources=Lead.SOURCES)


@app.route('/leads/<int:lead_id>/delete', methods=['POST'])
def lead_delete(lead_id):
    lead = db.get_or_404(Lead, lead_id)
    name = lead.name
    db.session.delete(lead)
    db.session.commit()
    flash(f'Lead "{name}" deleted.', 'info')
    return redirect(url_for('leads_list'))


# ---------------------------------------------------------------------------
# Routes – Call Logs
# ---------------------------------------------------------------------------

@app.route('/calls')
def calls_list():
    calls = (
        CallLog.query
        .order_by(CallLog.called_at.desc())
        .limit(100)
        .all()
    )
    return render_template('calls_list.html', calls=calls)


@app.route('/leads/<int:lead_id>/calls/new', methods=['POST'])
def call_log_new(lead_id):
    lead = db.get_or_404(Lead, lead_id)
    duration_raw = request.form.get('duration_minutes', '').strip()
    call = CallLog(
        lead_id=lead.id,
        called_at=_now(),
        duration_minutes=int(duration_raw) if duration_raw.isdigit() else None,
        outcome=request.form.get('outcome', 'Answered'),
        notes=request.form.get('notes', '').strip() or None,
    )
    db.session.add(call)

    # Update lead status if outcome implies it
    outcome = call.outcome
    if outcome == 'Callback Requested' and lead.status not in ('Converted', 'Do Not Call'):
        lead.status = 'In Progress'
    elif outcome == 'Answered' and lead.status == 'New':
        lead.status = 'In Progress'

    db.session.commit()
    flash('Call logged successfully.', 'success')
    return redirect(url_for('lead_detail', lead_id=lead.id))


@app.route('/calls/<int:call_id>/delete', methods=['POST'])
def call_log_delete(call_id):
    call = db.get_or_404(CallLog, call_id)
    lead_id = call.lead_id
    db.session.delete(call)
    db.session.commit()
    flash('Call log deleted.', 'info')
    return redirect(url_for('lead_detail', lead_id=lead_id))


# ---------------------------------------------------------------------------
# Routes – Follow-ups
# ---------------------------------------------------------------------------

@app.route('/follow-ups')
def follow_ups_list():
    pending = (
        FollowUp.query
        .filter_by(completed=False)
        .order_by(FollowUp.scheduled_at.asc())
        .all()
    )
    completed = (
        FollowUp.query
        .filter_by(completed=True)
        .order_by(FollowUp.completed_at.desc())
        .limit(20)
        .all()
    )
    return render_template('follow_ups_list.html', pending=pending, completed=completed)


@app.route('/leads/<int:lead_id>/follow-ups/new', methods=['POST'])
def follow_up_new(lead_id):
    lead = db.get_or_404(Lead, lead_id)
    scheduled_str = request.form.get('scheduled_at', '').strip()
    try:
        scheduled_at = datetime.strptime(scheduled_str, '%Y-%m-%dT%H:%M')
    except ValueError:
        flash('Invalid date/time for follow-up.', 'danger')
        return redirect(url_for('lead_detail', lead_id=lead.id))

    follow_up = FollowUp(
        lead_id=lead.id,
        scheduled_at=scheduled_at,
        reason=request.form.get('reason', '').strip() or None,
    )
    db.session.add(follow_up)
    db.session.commit()
    flash('Follow-up scheduled successfully.', 'success')
    return redirect(url_for('lead_detail', lead_id=lead.id))


@app.route('/follow-ups/<int:follow_up_id>/complete', methods=['POST'])
def follow_up_complete(follow_up_id):
    follow_up = db.get_or_404(FollowUp, follow_up_id)
    lead_id = follow_up.lead_id
    follow_up.completed = True
    follow_up.completed_at = _now()
    db.session.commit()
    flash('Follow-up marked as completed.', 'success')
    # Redirect to the lead's detail page when the request comes from there,
    # otherwise fall back to the follow-ups list – both destinations are
    # constructed via url_for so no user-controlled URL is ever followed.
    from_lead = request.form.get('from_lead') == '1'
    if from_lead:
        return redirect(url_for('lead_detail', lead_id=lead_id))
    return redirect(url_for('follow_ups_list'))


@app.route('/follow-ups/<int:follow_up_id>/delete', methods=['POST'])
def follow_up_delete(follow_up_id):
    follow_up = db.get_or_404(FollowUp, follow_up_id)
    lead_id = follow_up.lead_id
    db.session.delete(follow_up)
    db.session.commit()
    flash('Follow-up deleted.', 'info')
    # Always redirect back to the lead detail page (we know lead_id from the model).
    return redirect(url_for('lead_detail', lead_id=lead_id))


# ---------------------------------------------------------------------------
# API – simple JSON endpoints for dynamic UI
# ---------------------------------------------------------------------------

@app.route('/api/stats')
def api_stats():
    today_start = datetime.combine(date.today(), datetime.min.time())
    today_end = datetime.combine(date.today(), datetime.max.time())

    stats = {
        'total_leads': Lead.query.count(),
        'calls_today': CallLog.query.filter(
            CallLog.called_at >= today_start,
            CallLog.called_at <= today_end
        ).count(),
        'pending_follow_ups': FollowUp.query.filter_by(completed=False).count(),
        'converted_leads': Lead.query.filter_by(status='Converted').count(),
        'leads_by_status': {
            status: Lead.query.filter_by(status=status).count()
            for status in Lead.STATUSES
        },
        'calls_by_outcome': {
            outcome: CallLog.query.filter_by(outcome=outcome).count()
            for outcome in CallLog.OUTCOMES
        },
    }
    return jsonify(stats)


# ---------------------------------------------------------------------------
# App entry point
# ---------------------------------------------------------------------------

def create_tables():
    with app.app_context():
        db.create_all()


if __name__ == '__main__':
    create_tables()
    debug = os.environ.get('FLASK_DEBUG', '0') == '1'
    app.run(debug=debug)

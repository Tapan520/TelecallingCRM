/* Telecalling CRM – client-side enhancements */

document.addEventListener('DOMContentLoaded', function () {
  // Auto-dismiss flash alerts after 5 seconds
  document.querySelectorAll('.alert.alert-dismissible').forEach(function (el) {
    setTimeout(function () {
      var bsAlert = bootstrap.Alert.getOrCreateInstance(el);
      bsAlert.close();
    }, 5000);
  });

  // Highlight overdue follow-ups
  document.querySelectorAll('td[data-scheduled]').forEach(function (td) {
    var scheduled = new Date(td.dataset.scheduled);
    if (scheduled < new Date()) {
      td.closest('tr').classList.add('table-danger');
    }
  });
});

import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';

import { Ticket } from '../models/ticket.model';

@Service()
export class TicketService {
  private readonly apiUrl = 'https://localhost:7146/api/tickets';
  private readonly http = inject(HttpClient);

  getTickets(page = 1, pageSize = 10): Observable<Ticket[]> {
    return this.http.get<Ticket[]>(this.apiUrl, {
      params: {
        page,
        pageSize
      }
    });
  }
}

import { Component, OnInit, signal } from '@angular/core';
import { TicketService } from '../../services/ticket.service';
import { Ticket } from '../../models/ticket.model';
import { Router } from '@angular/router';

@Component({
  selector: 'app-ticket-list',
  standalone: false,
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.css',
})
export class TicketList implements OnInit {
  protected readonly tickets = signal<Ticket[]>([]);
  protected readonly isLoading = false;
  constructor(private readonly ticketService: TicketService,
    private readonly router: Router) {
  }

  ngOnInit(): void {
    this.ticketService.getTickets().subscribe({

      next: (tickets) => {
        this.tickets.set(tickets);
      }
      ,

      error: (err) => {
        console.error(err);
      }
    });
  }

  protected createTicket(): void {
    this.router.navigate(['/tickets/create']);
  }
}

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Dashboard } from './features/dashboard/pages/dashboard/dashboard';
import { TicketList } from './features/tickets/pages/ticket-list/ticket-list';
import { TicketCreate } from './features/tickets/pages/ticket-create/ticket-create';
const routes: Routes = [
  {
    path : '',
    component: Dashboard
  },
  {
    path: 'tickets',
    component: TicketList
  },
  {
    path: 'tickets/create',
    component: TicketCreate
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }

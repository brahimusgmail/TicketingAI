import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing-module';
import { AppComponent } from './app.component';
import { TicketList } from './features/tickets/pages/ticket-list/ticket-list';
import { Dashboard } from './features/dashboard/pages/dashboard/dashboard';
import { Navbar } from './shared/components/navbar/navbar';
import { provideHttpClient } from '@angular/common/http';
import { TicketCreate } from './features/tickets/pages/ticket-create/ticket-create';

@NgModule({
  declarations: [AppComponent, TicketList, Dashboard, Navbar, TicketCreate],
  imports: [BrowserModule, AppRoutingModule],
  providers: [provideBrowserGlobalErrorListeners(), provideHttpClient()],
  bootstrap: [AppComponent],
})
export class AppModule {}
